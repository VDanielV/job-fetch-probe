using System.Diagnostics;
using JobFetchProbe.Models.Details;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Services.Abstractions;

namespace JobFetchProbe.Services.Details;

/// <summary>
/// Step 2. Takes every job in the database without a description (see <see cref="IJobRepository.GetUnreadAsync"/>)
/// and, one by one: reads its page with <see cref="IJobService.ReadUrlAsync"/>, then stores the description or the
/// failure straight away. A stopped run loses nothing; the next run starts with what is still unread.
///
/// Politeness: one request at a time, a pause between requests, no quick retries, and a <see cref="CircuitBreaker"/>.
/// A refusal (401/403/429/999, sign-in page) opens the circuit at once; network failures and server errors open it
/// after a few in a row. While open, nothing is sent; after the cooldown one trial request (the job that opened the
/// circuit, tried once more) decides whether to continue. When the circuit has opened StopAfterOpenings times, the run
/// stops. A 404 or 410 marks only that job, as gone.
/// </summary>
public sealed class DetailsService(IJobRepository repository, IJobService jobs, TimeProvider time) : IDetailsService
{
    /// <summary>How often the time left is reported while the circuit is open.</summary>
    public static readonly TimeSpan WaitReportInterval = TimeSpan.FromSeconds(30);

    public async Task<DetailsRunResult> RunAsync(
        TimeSpan delay,
        CircuitBreakerOptions breakerOptions,
        IProgress<DetailsEvent>? progress = null,
        CancellationToken ct = default)
    {
        var unread = await repository.GetUnreadAsync(ct);
        var breaker = new CircuitBreaker(breakerOptions, time);
        var fetched = 0;
        var failed = new HashSet<string>(StringComparer.Ordinal);
        var attempted = new HashSet<string>(StringComparer.Ordinal); // distinct jobs tried in this run
        var requests = 0;
        string? stopped = null;
        var clock = Stopwatch.StartNew();
        var activeTime = TimeSpan.Zero; // time spent requesting and pausing, without circuit cooldowns

        Report(DetailsEventKind.Started, e => e with { Total = unread.Count, ToRead = unread.Count, Wait = delay, StopAfterOpenings = breakerOptions.StopAfterOpenings });

        for (var i = 0; i < unread.Count && stopped is null; i++)
        {
            var item = unread[i];

            // Circuit open: wait for the cooldown, reporting the time left, then allow one trial request.
            while (!breaker.TryAcquire())
            {
                // Whole seconds, rounded up: the timer may wake a few milliseconds before the cooldown ends, which
                // would otherwise send this loop round again (and again) without a real wait.
                var remaining = TimeSpan.FromSeconds(Math.Ceiling(breaker.Remaining.TotalSeconds));
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

            // Store the outcome before anything else, so it is kept whatever happens next.
            // Saving is not cancelled: Ctrl+C stops after the current job, never in the middle of a write.
            var wasHalfOpen = breaker.State == CircuitState.HalfOpen;
            if (result.Status == JobResultStatus.Read && result.Job is not null)
            {
                // Keep the listed ID: it is the reliable one (the page may lack a canonical link).
                await repository.SaveDescriptionAsync(result.Job with { JobId = item.JobId }, CancellationToken.None);
                failed.Remove(item.JobId);
                fetched++;
                breaker.RecordSuccess();
            }
            else
            {
                var gone = result.HttpStatus is 404 or 410;
                await repository.SaveFailureAsync(item.JobId, result.Status, gone, result.Message ?? string.Empty, CancellationToken.None);
                failed.Add(item.JobId);
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

            var left = unread.Count - attempted.Count;
            TimeSpan? eta = requests > 0 ? TimeSpan.FromTicks(activeTime.Ticks / requests * left) : null;
            Report(DetailsEventKind.Job, e => e with
            {
                Index = i + 1,
                Total = unread.Count,
                ToRead = left,
                Fetched = fetched,
                Failed = failed.Count,
                Job = item,
                Status = result.Status,
                Gone = result.HttpStatus is 404 or 410,
                Title = result.Job?.Title,
                Message = result.Message,
                Eta = eta,
            });

            if (wasHalfOpen && breaker.State == CircuitState.Closed)
            {
                Report(DetailsEventKind.CircuitClosed, e => e with { Openings = breaker.Openings });
            }

            if (breaker.State == CircuitState.Open)
            {
                if (breaker.Exhausted)
                {
                    stopped = $"the circuit breaker opened {breaker.Openings} time(s); last reason: {breaker.Reason}. " +
                              "Stopped so the site is not asked again; the next run continues where it stopped.";
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

        var pending = unread.Count - attempted.Count;
        if (stopped is not null)
        {
            Report(DetailsEventKind.Stopped, e => e with { Message = stopped, ToRead = pending, Fetched = fetched, Failed = failed.Count, Openings = breaker.Openings });
        }

        return new DetailsRunResult(
            unread.Count,
            fetched,
            failed.Count,
            pending,
            (await repository.GetUnreadAsync(CancellationToken.None)).Count,
            await repository.CountReadAsync(CancellationToken.None),
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
}
