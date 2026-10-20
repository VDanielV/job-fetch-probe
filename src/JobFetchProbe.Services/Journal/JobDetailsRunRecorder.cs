using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Abstractions.Journal;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Journal;

/// <summary>
/// Turns step 2 progress into journal writes: the run's state after each event, plus one entry per job and per
/// circuit breaker change. The "still waiting" reports while the circuit is open only refresh the state (the reader
/// computes the countdown from CircuitOpenUntil). Without a repository (the journal could not be started) it records
/// nothing.
/// </summary>
internal sealed class JobDetailsRunRecorder : IJobDetailsRunRecorder
{
    private readonly RunRecorderCore<JobDetailsJournalRun, JobDetailsJournalEntry> core;

    public JobDetailsRunRecorder(IJournalRepository? repository, JobDetailsJournalRun state, JournalOptions options, TimeProvider time, ILogger logger)
    {
        core = new RunRecorderCore<JobDetailsJournalRun, JobDetailsJournalEntry>(
            repository,
            JournalStep.JobDetails,
            state.RunId,
            state,
            (journal, run, entry, ct) => journal.SaveJobDetailsRunAsync(run, entry, ct),
            options,
            time,
            logger);
    }

    public long RunId
    {
        get
        {
            return core.RunId;
        }
    }

    /// <summary>The run as it stands; changed only inside <c>core.Change</c>.</summary>
    private JobDetailsJournalRun Run
    {
        get
        {
            return core.State;
        }
        set
        {
            core.State = value;
        }
    }

    public void Report(JobDetailsFetchEvent value)
    {
        core.Change(now =>
        {
            Run = Run with { LastSeenAt = now };
            switch (value.Kind)
            {
                case JobDetailsFetchEventKind.Started:
                    Run = Run with
                    {
                        Total = value.Total,
                        ToRead = value.ToRead,
                        StopAfterOpenings = value.StopAfterOpenings,
                        Message = $"{value.ToRead} job(s) to read",
                    };
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.Started) with { Message = Run.Message });
                    break;

                case JobDetailsFetchEventKind.Job:
                    Run = Run with
                    {
                        Total = value.Total,
                        ToRead = value.ToRead,
                        Fetched = value.Fetched,
                        Failed = value.Failed,
                        Requests = Run.Requests + 1,
                        EtaSeconds = Seconds(value.Eta),
                        Message = $"Job {value.Index} of {value.Total}",
                    };
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.Job));
                    break;

                case JobDetailsFetchEventKind.CircuitOpened:
                    Run = Run with
                    {
                        CircuitState = JournalCircuitState.Open,
                        CircuitOpenUntil = value.At + (value.Wait ?? TimeSpan.Zero),
                        Openings = value.Openings,
                        StopAfterOpenings = value.StopAfterOpenings,
                        Message = $"Circuit open: {value.Message}",
                    };
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.CircuitOpened) with { WaitSeconds = Seconds(value.Wait) });
                    break;

                case JobDetailsFetchEventKind.Waiting:
                    Run = Run with
                    {
                        CircuitState = JournalCircuitState.Open,
                        CircuitOpenUntil = value.At + (value.Wait ?? TimeSpan.Zero),
                    };
                    core.Save(null);
                    break;

                case JobDetailsFetchEventKind.CircuitHalfOpen:
                    Run = Run with
                    {
                        CircuitState = JournalCircuitState.HalfOpen,
                        CircuitOpenUntil = null,
                        Message = "Circuit half-open: one trial request",
                    };
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.CircuitHalfOpen));
                    break;

                case JobDetailsFetchEventKind.CircuitClosed:
                    Run = Run with { CircuitState = JournalCircuitState.Closed, CircuitOpenUntil = null, Message = "Circuit closed: normal pace" };
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.CircuitClosed));
                    break;

                case JobDetailsFetchEventKind.Assessed:
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.Assessed) with
                    {
                        Message = FitLine(value),
                        WaitSeconds = Seconds(value.Took),
                    });
                    break;

                case JobDetailsFetchEventKind.AssessOff:
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.AssessOff));
                    break;

                case JobDetailsFetchEventKind.Stopped:
                    Run = Run with { Message = $"Stopped: {value.Message}" };
                    core.Save(Entry(value, now, JobDetailsJournalEntryKind.Stopped));
                    break;
            }
        });
    }

    public async Task CompleteAsync(JobDetailsFetchResult result)
    {
        core.Change(now =>
        {
            var summary = $"Read {result.Fetched}, failed {result.Failed}, not tried {result.Pending}; " +
                          $"{result.StillUnread} still without a description, {result.TotalRead} stored in total.";
            if (result.Assessed + result.NotAssessed > 0)
            {
                summary += $" Fit scored for {result.Assessed} of {result.Assessed + result.NotAssessed} read.";
            }
            if (result.StoppedReason is not null)
            {
                summary = $"Stopped early ({result.StoppedReason.TrimEnd('.')}). {summary}";
            }

            // Stopped by the circuit breaker: its last trial request was refused, so the circuit ends open.
            var circuit = Run.CircuitState;
            if (Run.StopAfterOpenings > 0 && result.CircuitOpenings >= Run.StopAfterOpenings)
            {
                circuit = JournalCircuitState.Open;
            }
            var ended = JournalRunState.Completed;
            if (result.StoppedReason is not null)
            {
                ended = JournalRunState.Stopped;
            }
            Run = Run with
            {
                LastSeenAt = now,
                FinishedAt = now,
                State = ended,
                Total = result.Unread,
                ToRead = result.Pending,
                Fetched = result.Fetched,
                Failed = result.Failed,
                Requests = result.Requests,
                Openings = result.CircuitOpenings,
                CircuitState = circuit,
                CircuitOpenUntil = null,
                EtaSeconds = null,
                Message = summary,
                StillUnread = result.StillUnread,
                TotalRead = result.TotalRead,
            };
            core.Save(new JobDetailsJournalEntry(0, now, JobDetailsJournalEntryKind.Finished, 0, result.Unread, null, null, null, false, summary, null));
        });
        await core.FinishAsync();
    }

    public async Task FailAsync(Exception error)
    {
        core.Change(now =>
        {
            Run = Run with
            {
                LastSeenAt = now,
                FinishedAt = now,
                State = JournalRunState.Failed,
                EtaSeconds = null,
                Message = error.GetBaseException().Message,
                ErrorType = error.GetBaseException().GetType().FullName,
            };
            core.Save(new JobDetailsJournalEntry(0, now, JobDetailsJournalEntryKind.Finished, 0, Run.Total, null, null, null, false, Run.Message, null));
        });
        await core.FinishAsync();
    }

    /// <summary>A run disposed without a result ended unexpectedly: recorded as Failed.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!core.Finished)
        {
            await FailAsync(new InvalidOperationException("The run ended without reporting a result."));
        }
    }

    private static JobDetailsJournalEntry Entry(JobDetailsFetchEvent value, DateTimeOffset now, JobDetailsJournalEntryKind kind)
    {
        return new JobDetailsJournalEntry(0, now, kind, value.Index, value.Total, value.Job?.SourceJobId, value.Title ?? value.Job?.Title, value.Status, value.Gone, value.Message, null);
    }

    /// <summary>"Fit 78 · Apply", "Fit 46 · Skip · hard skip", or "Not scored: why".</summary>
    private static string FitLine(JobDetailsFetchEvent value)
    {
        if (value.Fit is not { } fit)
        {
            return "Not scored: " + (value.Message ?? "unknown reason");
        }
        var line = $"Fit {fit.Fit} · {fit.Decision}";
        if (fit.HardSkip)
        {
            line += " · hard skip";
        }
        return line;
    }

    private static int? Seconds(TimeSpan? span)
    {
        if (span is not { } value)
        {
            return null;
        }
        return (int)Math.Ceiling(value.TotalSeconds);
    }
}
