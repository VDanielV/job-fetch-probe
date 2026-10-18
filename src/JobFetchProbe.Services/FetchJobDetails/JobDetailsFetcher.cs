using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Search;
using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>
/// Step 2. Takes every job in the database without a description (see <see cref="IJobRepository.GetUnreadAsync"/>)
/// and, one by one: reads its page with <see cref="IJobService.ReadUrlAsync"/>, then stores the description or the
/// failure straight away. A stopped run loses nothing; the next run starts with what is still unread.
///
/// Politeness: one request at a time, a pause between requests, no quick retries, and a <see cref="CircuitBreaker"/>.
/// A refusal (401/403/429/999, sign-in page) opens the circuit at once; network failures and server errors open it
/// after a few in a row. While open, nothing is sent; after the cooldown one trial request (the job that opened the
/// circuit, tried once more) decides whether to continue. When the circuit has opened StopAfterOpenings times, the run
/// stops. A 404 or 410 marks only that job, as gone. The pause and the circuit breaker come from
/// <see cref="FetchJobDetailsSettings"/>.
/// </summary>
public sealed class JobDetailsFetcher(
    IJobRepository repository,
    IJobService jobs,
    IOptions<FetchJobDetailsSettings> settings,
    TimeProvider time) : IJobDetailsFetcher
{
    /// <summary>How often the time left is reported while the circuit is open.</summary>
    public static readonly TimeSpan WaitReportInterval = TimeSpan.FromSeconds(30);

    public async Task<JobDetailsFetchResult> FetchAsync(IProgress<JobDetailsFetchEvent>? progress = null, CancellationToken ct = default)
    {
        var delay = TimeSpan.FromSeconds(settings.Value.DelaySeconds);
        var breakerOptions = settings.Value.CircuitBreaker.ToOptions();
        var unread = await repository.GetUnreadAsync(ct);
        var run = new JobDetailsFetchState(unread, new CircuitBreaker(breakerOptions, time), progress, time);

        run.Report(JobDetailsFetchEventKind.Started, e => e with
        {
            Total = unread.Count,
            ToRead = unread.Count,
            Wait = delay,
            StopAfterOpenings = breakerOptions.StopAfterOpenings,
            FailureThreshold = breakerOptions.FailureThreshold,
            Cooldown = breakerOptions.Cooldown,
            MaxCooldown = breakerOptions.MaxCooldown,
        });

        for (var i = 0; i < unread.Count && run.Stopped is null; i++)
        {
            if (ct.IsCancellationRequested)
            {
                run.Stopped = JobDetailsFetchState.CancelledReason;
                break;
            }
            var job = unread[i];
            if (!await WaitForCircuitAsync(run, ct) || !await PauseBeforeRequestAsync(run, delay, ct))
            {
                break;
            }

            var result = await RequestAsync(run, job, ct);
            if (result is null)
            {
                break;
            }

            var wasHalfOpen = run.Breaker.State == CircuitState.HalfOpen;
            await StoreAsync(run, job, result);
            FetchOutcome.TellBreaker(run.Breaker, job, result);
            ReportJob(run, i, job, result);

            if (AfterResult(run, wasHalfOpen))
            {
                // After the cooldown, the trial request is this same job again, so it is not skipped.
                i--;
            }
        }

        if (run.Stopped is not null)
        {
            run.Report(JobDetailsFetchEventKind.Stopped, e => e with
            {
                Message = run.Stopped,
                ToRead = run.Left,
                Fetched = run.Fetched,
                Failed = run.Failed.Count,
                Openings = run.Breaker.Openings,
            });
        }

        return new JobDetailsFetchResult(
            unread.Count,
            run.Fetched,
            run.Failed.Count,
            run.Left,
            (await repository.GetUnreadAsync(CancellationToken.None)).Count,
            await repository.CountReadAsync(CancellationToken.None),
            run.Requests,
            run.Breaker.Openings,
            run.Clock.Elapsed,
            run.Stopped);
    }

    /// <summary>While the circuit is open: waits for the cooldown, reporting the time left, until one trial request is allowed.</summary>
    /// <returns>False if cancelled.</returns>
    private async Task<bool> WaitForCircuitAsync(JobDetailsFetchState run, CancellationToken ct)
    {
        while (!run.Breaker.TryAcquire())
        {
            // Whole seconds, rounded up: the timer may wake a few milliseconds before the cooldown ends, which
            // would otherwise send this loop round again (and again) without a real wait.
            var remaining = TimeSpan.FromSeconds(Math.Ceiling(run.Breaker.Remaining.TotalSeconds));
            run.Report(JobDetailsFetchEventKind.Waiting, e => e with
            {
                Wait = remaining,
                Openings = run.Breaker.Openings,
                StopAfterOpenings = run.Breaker.Options.StopAfterOpenings,
            });

            var wait = WaitReportInterval;
            if (remaining < WaitReportInterval)
            {
                wait = remaining;
            }
            if (!await PauseAsync(wait, ct))
            {
                run.Stopped = JobDetailsFetchState.CancelledReason;
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The trial request after a cooldown goes at once (and is reported); any other request after the first waits
    /// <paramref name="delay"/>, counted as active time for the estimate.
    /// </summary>
    /// <returns>False if cancelled.</returns>
    private async Task<bool> PauseBeforeRequestAsync(JobDetailsFetchState run, TimeSpan delay, CancellationToken ct)
    {
        if (run.Breaker.State == CircuitState.HalfOpen)
        {
            run.Report(JobDetailsFetchEventKind.CircuitHalfOpen, e => e with { Openings = run.Breaker.Openings });
            return true;
        }
        if (run.Requests == 0)
        {
            return true;
        }

        var pauseStarted = run.Clock.Elapsed;
        if (!await PauseAsync(delay, ct))
        {
            run.Stopped = JobDetailsFetchState.CancelledReason;
            return false;
        }
        run.Clock.AddActiveSince(pauseStarted);
        return true;
    }

    /// <summary>Reads the job's page.</summary>
    /// <returns>Null if cancelled.</returns>
    private async Task<JobResult?> RequestAsync(JobDetailsFetchState run, JobListing job, CancellationToken ct)
    {
        var requestStarted = run.Clock.Elapsed;
        JobResult result;
        try
        {
            run.Requests++;
            result = await jobs.ReadUrlAsync(job.Url, ct);
        }
        catch (OperationCanceledException)
        {
            run.Stopped = JobDetailsFetchState.CancelledReason;
            return null;
        }
        run.Clock.AddActiveSince(requestStarted);
        run.Attempted.Add(job.JobId);
        return result;
    }

    /// <summary>
    /// Stores the outcome before anything else, so it is kept whatever happens next. Saving is not cancelled:
    /// A stop (the Stop button, or the API stopping) takes effect after the current job, never in the middle of a write.
    /// </summary>
    private async Task StoreAsync(JobDetailsFetchState run, JobListing job, JobResult result)
    {
        if (FetchOutcome.IsRead(result))
        {
            // Keep the listed ID: it is the reliable one (the page may lack a canonical link).
            await repository.SaveDescriptionAsync(result.Job! with { JobId = job.JobId }, CancellationToken.None);
            run.Failed.Remove(job.JobId);
            run.Fetched++;
        }
        else
        {
            await repository.SaveFailureAsync(job.JobId, result.Status, FetchOutcome.IsGone(result), result.Message ?? string.Empty, CancellationToken.None);
            run.Failed.Add(job.JobId);
        }
    }

    private static void ReportJob(JobDetailsFetchState run, int index, JobListing job, JobResult result)
    {
        var left = run.Left;
        var eta = run.Clock.For(run.Requests, left);
        run.Report(JobDetailsFetchEventKind.Job, e => e with
        {
            Index = index + 1,
            Total = run.Unread.Count,
            ToRead = left,
            Fetched = run.Fetched,
            Failed = run.Failed.Count,
            Job = job,
            Status = result.Status,
            Gone = FetchOutcome.IsGone(result),
            Title = result.Job?.Title,
            Message = result.Message,
            Eta = eta,
        });
    }

    /// <summary>
    /// Reports what the result did to the circuit: closed again after a trial, or opened. When it has opened
    /// StopAfterOpenings times, the run stops.
    /// </summary>
    /// <returns>True when the circuit opened and the same job is to be tried again after the cooldown.</returns>
    private static bool AfterResult(JobDetailsFetchState run, bool wasHalfOpen)
    {
        var breaker = run.Breaker;
        if (wasHalfOpen && breaker.State == CircuitState.Closed)
        {
            run.Report(JobDetailsFetchEventKind.CircuitClosed, e => e with { Openings = breaker.Openings });
        }

        if (breaker.State != CircuitState.Open)
        {
            return false;
        }
        if (breaker.Exhausted)
        {
            run.Stopped = $"the circuit breaker opened {breaker.Openings} time(s); last reason: {breaker.Reason}. " +
                          "Stopped so the site is not asked again; the next run continues where it stopped.";
            return false;
        }

        run.Report(JobDetailsFetchEventKind.CircuitOpened, e => e with
        {
            Message = breaker.Reason,
            Wait = breaker.LastCooldown,
            Openings = breaker.Openings,
            StopAfterOpenings = breaker.Options.StopAfterOpenings,
        });
        return true;
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
