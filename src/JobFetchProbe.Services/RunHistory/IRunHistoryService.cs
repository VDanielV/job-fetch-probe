using JobFetchProbe.Models.RunHistory;

namespace JobFetchProbe.Services.RunHistory;

/// <summary>
/// The history of past acquisitions, read only: one line per acquisition, and every job link of one with what step 2
/// did with it. Throws <c>JobStoreUnavailableException</c> while the database does not exist yet.
/// </summary>
public interface IRunHistoryService
{
    /// <summary>One page of acquisitions, newest first; those that did nothing only when <paramref name="includeEmpty"/>.</summary>
    Task<RunHistoryPage> GetPageAsync(int page, int pageSize, bool includeEmpty, CancellationToken ct = default);

    /// <summary>One acquisition, by its step 1 run's journal ID; null if unknown.</summary>
    Task<RunHistoryLine?> GetRunAsync(long runId, CancellationToken ct = default);

    /// <summary>The job links of an acquisition, by its step 1 run's journal ID; null if unknown.</summary>
    Task<RunJobLinks?> GetJobLinksAsync(long runId, CancellationToken ct = default);

    /// <summary>The job links of a listing no journal run refers to (stored before the journal existed); null if unknown.</summary>
    Task<RunJobLinks?> GetListingJobLinksAsync(string listingId, CancellationToken ct = default);
}
