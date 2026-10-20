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
///
/// After a description is stored, the job's follow-up runs (<see cref="IJobReadFollowUp"/>: scoring its fit) before
/// the pause, so its time adds to the gap between two requests. A failed follow-up never stops the run and never
/// touches the circuit breaker; after FitFailuresBeforeOff failures in a row it is off for the rest of the run.
/// </summary>
public sealed class JobDetailsFetcher(
    IJobRepository repository,
    IJobService jobs,
    IOptions<FetchJobDetailsSettings> settings,
    TimeProvider time,
    IEnumerable<IJobReadFollowUp> followUps) : IJobDetailsFetcher
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
            var readId = await StoreAsync(run, job, result);
            FetchOutcome.TellBreaker(run.Breaker, job, result);
            ReportJob(run, i, job, result);
            if (readId is { } jobId && !await FollowUpAsync(run, i, job, jobId, ct))
            {
                break;
            }

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
            run.Stopped,
            run.Assessed,
            run.NotAssessed);
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
        run.Attempted.Add(job.SourceJobId);
        return result;
    }

    /// <summary>
    /// Stores the outcome before anything else, so it is kept whatever happens next. Saving is not cancelled:
    /// A stop (the Stop button, or the API stopping) takes effect after the current job, never in the middle of a write.
    /// </summary>
    /// <returns>The job's app ID when its description was stored; null after a failure.</returns>
    private async Task<long?> StoreAsync(JobDetailsFetchState run, JobListing job, JobResult result)
    {
        if (FetchOutcome.IsRead(result))
        {
            // Keep the listed ID: it is the reliable one (the page may lack a canonical link).
            var id = await repository.SaveDescriptionAsync(result.Job! with { Source = job.Source, SourceJobId = job.SourceJobId }, CancellationToken.None);
            run.Failed.Remove(job.SourceJobId);
            run.Fetched++;
            return id;
        }

        await repository.SaveFailureAsync(job.Source, job.SourceJobId, result.Status, FetchOutcome.IsGone(result), result.Message ?? string.Empty, CancellationToken.None);
        run.Failed.Add(job.SourceJobId);
        return null;
    }

    /// <summary>
    /// The follow-up of a job just read (scoring its fit), when it is on. Its time counts as active time for the
    /// estimate. A failure is reported and counted; enough in a row switch it off for the rest of the run.
    /// </summary>
    /// <returns>False if the run was stopped meanwhile.</returns>
    private async Task<bool> FollowUpAsync(JobDetailsFetchState run, int index, JobListing job, long jobId, CancellationToken ct)
    {
        var followUp = followUps.FirstOrDefault();
        if (followUp is null || !settings.Value.AssessFit)
        {
            return true;
        }
        if (run.AssessOff)
        {
            run.NotAssessed++;
            return true;
        }

        var started = run.Clock.Elapsed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        JobReadFollowUpResult outcome;
        try
        {
            outcome = await followUp.AfterReadAsync(jobId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Stopped = JobDetailsFetchState.CancelledReason;
            return false;
        }
        catch (OperationCanceledException ex)
        {
            outcome = new JobReadFollowUpResult(null, "the local model did not answer in time: " + ex.Message);
        }
        run.Clock.AddActiveSince(started);

        if (outcome.Fit is not null)
        {
            run.Assessed++;
            run.AssessFailuresInRow = 0;
        }
        else
        {
            run.NotAssessed++;
            run.AssessFailuresInRow++;
        }
        run.Report(JobDetailsFetchEventKind.Assessed, e => e with
        {
            Index = index + 1,
            Total = run.Unread.Count,
            Job = job,
            Fit = outcome.Fit,
            Took = watch.Elapsed,
            Message = outcome.Failure,
        });

        if (outcome.Fit is null && run.AssessFailuresInRow >= settings.Value.FitFailuresBeforeOff)
        {
            run.AssessOff = true;
            run.Report(JobDetailsFetchEventKind.AssessOff, e => e with
            {
                Message = $"{run.AssessFailuresInRow} scores failed in a row; scoring is off for the rest of this run (is Ollama running?)",
            });
        }
        return true;
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
