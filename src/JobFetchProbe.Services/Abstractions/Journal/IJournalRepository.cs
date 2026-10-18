using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Services.Abstractions.Journal;

/// <summary>
/// Where the journal is kept. Implemented by the data layer. Writing is used by the process that fetches (each call
/// stores the run's current state, and optionally one new entry); reading by anyone, from another process.
/// </summary>
public interface IJournalRepository
{
    /// <summary>False while the database does not exist yet.</summary>
    bool IsAvailable { get; }

    /// <summary>Stores a new step 1 run (its state as given) and returns its id.</summary>
    Task<long> StartJobListingRunAsync(JobListingJournalRun run, CancellationToken ct = default);

    /// <summary>Overwrites the step 1 run's state with <paramref name="run"/> and adds <paramref name="entry"/>, if any.</summary>
    Task SaveJobListingRunAsync(JobListingJournalRun run, JobListingJournalEntry? entry, CancellationToken ct = default);

    /// <summary>Stores a new step 2 run (its state as given) and returns its id.</summary>
    Task<long> StartJobDetailsRunAsync(JobDetailsJournalRun run, CancellationToken ct = default);

    /// <summary>Overwrites the step 2 run's state with <paramref name="run"/> and adds <paramref name="entry"/>, if any.</summary>
    Task SaveJobDetailsRunAsync(JobDetailsJournalRun run, JobDetailsJournalEntry? entry, CancellationToken ct = default);

    /// <summary>The sign of life: only the run's LastSeenAt changes.</summary>
    Task TouchAsync(JournalStep step, long runId, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>Runs of the step still marked Running.</summary>
    Task<IReadOnlyList<JournalRunSummary>> GetRunningAsync(JournalStep step, CancellationToken ct = default);

    /// <summary>Marks a run Abandoned, with the reason.</summary>
    Task MarkAbandonedAsync(JournalStep step, long runId, DateTimeOffset at, string message, CancellationToken ct = default);

    /// <summary>Deletes the step's older runs (and their entries), keeping the newest <paramref name="keep"/>.</summary>
    Task PruneAsync(JournalStep step, int keep, CancellationToken ct = default);

    /// <summary>The newest runs of both steps, newest first.</summary>
    Task<IReadOnlyList<JournalRunSummary>> GetRecentRunsAsync(int take, CancellationToken ct = default);

    /// <summary>A step 1 run with its entries whose id is greater than <paramref name="afterEntryId"/>; null if unknown.</summary>
    Task<JobListingJournalRun?> GetJobListingRunAsync(long runId, long afterEntryId, CancellationToken ct = default);

    /// <summary>A step 2 run with its entries whose id is greater than <paramref name="afterEntryId"/>; null if unknown.</summary>
    Task<JobDetailsJournalRun?> GetJobDetailsRunAsync(long runId, long afterEntryId, CancellationToken ct = default);
}
