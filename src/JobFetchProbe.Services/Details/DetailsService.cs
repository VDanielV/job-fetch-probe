using System.Diagnostics;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;

namespace JobFetchProbe.Services.Details;

/// <summary>
/// The details step. For each job in the listing, in order:
/// - already in the details file from an earlier run → skipped, no request;
/// - otherwise read with the job step's <see cref="IJobService.ReadUrlAsync"/> (same client, same parser);
/// - the details file is rewritten after every job, so nothing is lost if the run stops.
///
/// Politeness: one request at a time, a pause between requests, no quick retries, and a
/// <see cref="CircuitBreaker"/>. A refusal (401/403/429/999, sign-in page) opens the circuit at once; network
/// failures and server errors open it after a few in a row. While open, nothing is sent; after the cooldown one
/// trial request (the job that opened the circuit, tried once more) decides whether to continue. When the circuit has opened StopAfterOpenings times, the run stops
/// and the remaining jobs stay pending for the next run. A 404 only marks that one job as failed.
/// </summary>
public sealed class DetailsService(
    IListingStore listings,
    IDetailsRepository repository,
    IJobService jobs,
    TimeProvider time) : IDetailsService
{
    /// <summary>How often the time left is reported while the circuit is open.</summary>
    public static readonly TimeSpan WaitReportInterval = TimeSpan.FromSeconds(30);

    public async Task<DetailsRunResult> RunAsync(
        string listingFile,
        TimeSpan delay,
        CircuitBreakerOptions breakerOptions,
        IProgress<DetailsEvent>? progress = null,
        CancellationToken ct = default)
    {
        var listing = await listings.ReadAsync(listingFile, ct)
                      ?? throw new InvalidDataException($"{Path.GetFileName(listingFile)} is not a listing file.");

        var previous = await repository.LoadAsync(listing.BatchId, ct);
        var done = new Dictionary<string, JobPosting>(StringComparer.Ordinal);
        foreach (var job in previous?.Jobs ?? [])
        {
            done.TryAdd(job.JobId, job);
        }

        var breaker = new CircuitBreaker(breakerOptions, time);
        var failures = new Dictionary<string, JobFailure>(StringComparer.Ordinal);
        var alreadyDone = listing.Jobs.Count(j => done.ContainsKey(j.JobId));
        var toRead = listing.Jobs.Count - alreadyDone;
        var fetched = 0;
        var requests = 0;
        var attempted = new HashSet<string>(StringComparer.Ordinal); // distinct jobs tried in this run
        string? stopped = null;
        string? savedTo = null;
        var clock = Stopwatch.StartNew();
        var activeTime = TimeSpan.Zero; // time spent requesting and pausing, without circuit cooldowns

        Report(DetailsEventKind.Started, e => e with { Listed = listing.Jobs.Count, AlreadyDone = alreadyDone, ToRead = toRead, Wait = delay, StopAfterOpenings = breakerOptions.StopAfterOpenings });

        for (var i = 0; i < listing.Jobs.Count && stopped is null; i++)
        {
            var item = listing.Jobs[i];
            if (done.ContainsKey(item.JobId))
            {
                continue;
            }

            // Circuit open: wait for the cooldown, reporting the time left, then allow one trial request.
            while (!breaker.TryAcquire())
            {
                var remaining = breaker.Remaining;
                Report(DetailsEventKind.Waiting, e => e with { Wait = remaining, Openings = breaker.Openings, StopAfterOpenings = breakerOptions.StopAfterOpenings });
                if (!await PauseAsync(remaining < WaitReportInterval ? remaining : WaitReportInterval, ct))
                {
                    stopped = "cancelled by the user";
                    break;
                }
            }
            if (stopped is not null)
            {
                break;
            }
            if (breaker.State == CircuitState.HalfOpen)
            {
                Report(DetailsEventKind.CircuitHalfOpen, e => e with { Openings = breaker.Openings });
            }
            else if (requests > 0)
            {
                var pauseStarted = clock.Elapsed;
                if (!await PauseAsync(delay, ct))
                {
                    stopped = "cancelled by the user";
                    break;
                }
                activeTime += clock.Elapsed - pauseStarted;
            }

            var requestStarted = clock.Elapsed;
            JobResult result;
            try
            {
                requests++;
                result = await jobs.ReadUrlAsync(item.Url, ct);
            }
            catch (OperationCanceledException)
            {
                stopped = "cancelled by the user";
                break;
            }
            activeTime += clock.Elapsed - requestStarted;
            attempted.Add(item.JobId);

            var wasHalfOpen = breaker.State == CircuitState.HalfOpen;
            if (result.Status == JobResultStatus.Read && result.Job is not null)
            {
                // Keep the listing's ID: it is the reliable one (the page may lack a canonical link).
                done[item.JobId] = result.Job with { JobId = item.JobId };
                failures.Remove(item.JobId);
                fetched++;
                breaker.RecordSuccess();
            }
            else
            {
                failures[item.JobId] = new JobFailure(item.JobId, item.Url, result.Status.ToString(), result.Message ?? string.Empty);
                if (result.Status == JobResultStatus.Blocked)
                {
                    breaker.RecordRefusal($"{item.Url.Host} refused the request for job {item.JobId} (status {result.HttpStatus}: refusal or sign-in page)");
                }
                else if (result.HttpStatus is 0 or >= 500)
                {
                    breaker.RecordFailure($"{item.Url.Host} is failing: {result.Message}");
                }
                else
                {
                    breaker.RecordNeutral(); // e.g. 404: this job is gone, the site itself is fine
                }
            }

            var left = toRead - attempted.Count;
            TimeSpan? eta = requests > 0 ? TimeSpan.FromTicks(activeTime.Ticks / requests * left) : null;
            Report(DetailsEventKind.Job, e => e with
            {
                Index = i + 1,
                Listed = listing.Jobs.Count,
                ToRead = left,
                Fetched = fetched,
                Failed = failures.Count,
                Listing = item,
                Status = result.Status,
                Title = result.Job?.Title,
                Message = result.Message,
                Eta = eta,
            });

            savedTo = await SaveAsync(listing, listingFile, done, failures, stopped: null);

            if (wasHalfOpen && breaker.State == CircuitState.Closed)
            {
                Report(DetailsEventKind.CircuitClosed, e => e with { Openings = breaker.Openings });
            }

            if (breaker.State == CircuitState.Open)
            {
                if (breaker.Exhausted)
                {
                    stopped = $"the circuit breaker opened {breaker.Openings} time(s); last reason: {breaker.Reason}. " +
                              "Stopped so the site is not asked again; run 'details' later to continue where it stopped.";
                }
                else
                {
                    // After the cooldown, the trial request is this same job again, so it is not skipped.
                    i--;
                    Report(DetailsEventKind.CircuitOpened, e => e with
                    {
                        Message = breaker.Reason,
                        Wait = breaker.LastCooldown,
                        Openings = breaker.Openings,
                        StopAfterOpenings = breakerOptions.StopAfterOpenings,
                    });
                }
            }
        }

        // Final write: records the stop reason (or clears it) even when no request was made in this run.
        savedTo = await SaveAsync(listing, listingFile, done, failures, stopped);
        var pending = listing.Jobs.Count(j => !done.ContainsKey(j.JobId) && !failures.ContainsKey(j.JobId));

        if (stopped is not null)
        {
            Report(DetailsEventKind.Stopped, e => e with { Message = stopped, ToRead = pending, Fetched = fetched, Failed = failures.Count, Openings = breaker.Openings });
        }

        return new DetailsRunResult(
            listing.BatchId,
            savedTo,
            listing.Jobs.Count,
            alreadyDone,
            fetched,
            failures.Count,
            pending,
            listing.Jobs.Count(j => done.ContainsKey(j.JobId)),
            requests,
            breaker.Openings,
            clock.Elapsed,
            stopped);

        void Report(DetailsEventKind kind, Func<DetailsEvent, DetailsEvent> fill) =>
            progress?.Report(fill(new DetailsEvent(kind, time.GetLocalNow())));
    }

    /// <returns>False if cancelled.</returns>
    private async Task<bool> PauseAsync(TimeSpan duration, CancellationToken ct)
    {
        if (duration <= TimeSpan.Zero)
        {
            return !ct.IsCancellationRequested;
        }
        try
        {
            await Task.Delay(duration, time, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<string> SaveAsync(
        JobSearchBatch listing,
        string listingFile,
        Dictionary<string, JobPosting> done,
        Dictionary<string, JobFailure> failures,
        string? stopped)
    {
        var details = new JobDetailsBatch
        {
            BatchId = listing.BatchId,
            ListingFile = listingFile,
            UpdatedAt = time.GetLocalNow(),
            Listed = listing.Jobs.Count,
            Pending = listing.Jobs.Count(j => !done.ContainsKey(j.JobId) && !failures.ContainsKey(j.JobId)),
            StoppedReason = stopped,
            // Listing order, then any jobs kept from an earlier run that are no longer listed.
            Jobs = listing.Jobs.Where(j => done.ContainsKey(j.JobId)).Select(j => done[j.JobId])
                .Concat(done.Values.Where(d => listing.Jobs.All(j => j.JobId != d.JobId)))
                .ToList(),
            Failures = failures.Values.ToList(),
        };

        // Saving is not cancelled: a half-written file would lose the progress made so far.
        return await repository.SaveAsync(details, CancellationToken.None);
    }
}
