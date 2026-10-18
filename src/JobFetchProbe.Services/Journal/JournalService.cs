using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Abstractions.Journal;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Journal;

/// <summary>
/// All journal operations, over <see cref="IJournalRepository"/> (the data layer). Writing goes through recorders
/// that queue their writes in the background; reading applies the Abandoned rule.
/// </summary>
public sealed class JournalService(
    IJournalRepository repository,
    JournalOptions options,
    TimeProvider time,
    ILogger<JournalService> logger) : IJournalService
{
    public const int MaxTake = 50;

    public async Task<IJobListingRunRecorder> StartJobListingRunAsync(JournalStarter startedBy, CancellationToken ct = default)
    {
        var now = time.GetLocalNow();
        var run = new JobListingJournalRun
        {
            StartedBy = startedBy,
            StartedAt = now,
            LastSeenAt = now,
            State = JournalRunState.Running,
            Message = "Started",
        };
        try
        {
            await PrepareAsync(JournalStep.JobListing, ct);
            run = run with { RunId = await repository.StartJobListingRunAsync(run, ct) };
            return new JobListingRunRecorder(repository, run, options, time, logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Journal: step 1 run not recorded; the fetch continues. {Error}", ex.GetBaseException().Message);
            return new JobListingRunRecorder(null, run, options, time, logger);
        }
    }

    public async Task<IJobDetailsRunRecorder> StartJobDetailsRunAsync(JournalStarter startedBy, CancellationToken ct = default)
    {
        var now = time.GetLocalNow();
        var run = new JobDetailsJournalRun
        {
            StartedBy = startedBy,
            StartedAt = now,
            LastSeenAt = now,
            State = JournalRunState.Running,
            CircuitState = JournalCircuitState.Closed,
            Message = "Started",
        };
        try
        {
            await PrepareAsync(JournalStep.JobDetails, ct);
            run = run with { RunId = await repository.StartJobDetailsRunAsync(run, ct) };
            return new JobDetailsRunRecorder(repository, run, options, time, logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Journal: step 2 run not recorded; the fetch continues. {Error}", ex.GetBaseException().Message);
            return new JobDetailsRunRecorder(null, run, options, time, logger);
        }
    }

    public async Task<IReadOnlyList<JournalRunSummary>> GetRecentRunsAsync(int take, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, MaxTake);
        EnsureAvailable();

        var runs = await repository.GetRecentRunsAsync(take, ct);
        var now = time.GetLocalNow();
        var result = new List<JournalRunSummary>(runs.Count);
        foreach (var run in runs)
        {
            if (IsAbandoned(run.State, run.LastSeenAt, now))
            {
                result.Add(run with { State = JournalRunState.Abandoned, Message = AbandonedMessage(run.LastSeenAt) });
            }
            else
            {
                result.Add(run);
            }
        }
        return result;
    }

    public async Task<JobListingJournalRun?> GetJobListingRunAsync(long runId, long afterEntryId, CancellationToken ct = default)
    {
        EnsureAvailable();
        var run = await repository.GetJobListingRunAsync(runId, Math.Max(0, afterEntryId), ct);
        if (run is not null && IsAbandoned(run.State, run.LastSeenAt, time.GetLocalNow()))
        {
            return run with { State = JournalRunState.Abandoned, Message = AbandonedMessage(run.LastSeenAt) };
        }
        return run;
    }

    public async Task<JobDetailsJournalRun?> GetJobDetailsRunAsync(long runId, long afterEntryId, CancellationToken ct = default)
    {
        EnsureAvailable();
        var run = await repository.GetJobDetailsRunAsync(runId, Math.Max(0, afterEntryId), ct);
        if (run is not null && IsAbandoned(run.State, run.LastSeenAt, time.GetLocalNow()))
        {
            return run with { State = JournalRunState.Abandoned, Message = AbandonedMessage(run.LastSeenAt), EtaSeconds = null };
        }
        return run;
    }

    public async Task<bool> IsAnyRunActiveAsync(CancellationToken ct = default)
    {
        if (!repository.IsAvailable)
        {
            return false;
        }

        var now = time.GetLocalNow();
        foreach (var step in new[] { JournalStep.JobListing, JournalStep.JobDetails })
        {
            foreach (var running in await repository.GetRunningAsync(step, ct))
            {
                if (!IsAbandoned(running.State, running.LastSeenAt, now))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Before a new run: runs of the same step left Running by a process that died are marked Abandoned, and old runs are deleted.</summary>
    private async Task PrepareAsync(JournalStep step, CancellationToken ct)
    {
        var now = time.GetLocalNow();
        foreach (var running in await repository.GetRunningAsync(step, ct))
        {
            if (IsAbandoned(running.State, running.LastSeenAt, now))
            {
                await repository.MarkAbandonedAsync(step, running.RunId, now, AbandonedMessage(running.LastSeenAt), ct);
            }
        }

        // TODO: review the retention later. Keeping the last runs per step bounds the journal in normal use, but every
        // run adds entries, and a process that fails over and over keeps adding runs: decide on a limit by age or size
        // (and maybe a cleanup outside the run) so the database cannot grow out of control.
        await repository.PruneAsync(step, Math.Max(0, options.KeepRunsPerStep - 1), ct);
    }

    private bool IsAbandoned(JournalRunState state, DateTimeOffset lastSeenAt, DateTimeOffset now) =>
        state == JournalRunState.Running && now - lastSeenAt > options.AbandonedAfter;

    private static string AbandonedMessage(DateTimeOffset lastSeenAt) =>
        $"No sign of life since {lastSeenAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}: the process ended without finishing the run.";

    private void EnsureAvailable()
    {
        if (!repository.IsAvailable)
        {
            throw new JournalUnavailableException();
        }
    }
}
