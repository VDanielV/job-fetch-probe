using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Services.Journal;

/// <summary>
/// The journal of the two steps: what a run is doing and what it did, written while it runs and readable from any
/// other process at the same time. Starting a run gives a recorder for the process that fetches; the read methods are
/// for everyone else (the API). A run that shows no sign of life for <see cref="JournalOptions.AbandonedAfter"/> is
/// reported as Abandoned: readers stop following it.
/// </summary>
public interface IJournalService
{
    /// <summary>Starts recording a step 1 run. Never throws: if the journal cannot be written, the recorder records nothing.</summary>
    Task<IJobListingRunRecorder> StartJobListingRunAsync(JournalStarter startedBy, CancellationToken ct = default);

    /// <summary>Starts recording a step 2 run. Never throws: if the journal cannot be written, the recorder records nothing.</summary>
    Task<IJobDetailsRunRecorder> StartJobDetailsRunAsync(JournalStarter startedBy, CancellationToken ct = default);

    /// <summary>
    /// True when a run of either step is still Running and showed a sign of life in the last
    /// <see cref="JournalOptions.AbandonedAfter"/>, whichever process started it. False when the database does not exist yet.
    /// </summary>
    Task<bool> IsAnyRunActiveAsync(CancellationToken ct = default);

    /// <summary>The newest runs of both steps, newest first.</summary>
    /// <param name="take">1 to <see cref="JournalService.MaxTake"/>.</param>
    /// <exception cref="JournalUnavailableException">The database does not exist yet.</exception>
    Task<IReadOnlyList<JournalRunSummary>> GetRecentRunsAsync(int take, CancellationToken ct = default);

    /// <summary>A step 1 run and its entries after <paramref name="afterEntryId"/> (0: all); null if there is no such run.</summary>
    /// <exception cref="JournalUnavailableException">The database does not exist yet.</exception>
    Task<JobListingJournalRun?> GetJobListingRunAsync(long runId, long afterEntryId, CancellationToken ct = default);

    /// <summary>A step 2 run and its entries after <paramref name="afterEntryId"/> (0: all); null if there is no such run.</summary>
    /// <exception cref="JournalUnavailableException">The database does not exist yet.</exception>
    Task<JobDetailsJournalRun?> GetJobDetailsRunAsync(long runId, long afterEntryId, CancellationToken ct = default);
}
