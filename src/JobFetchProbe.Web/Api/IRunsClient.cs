using JobFetchProbe.Models.RunHistory;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The calls the web app makes to the API's Runs controller, for the history of past acquisitions. They throw
/// <see cref="ApiUnavailableException"/> when the API is not reachable and <see cref="ApiErrorException"/> for an error.
/// </summary>
public interface IRunsClient
{
    /// <summary>GET api/Runs: one page of past acquisitions, newest first; null while there is no database yet.</summary>
    Task<RunHistoryPage?> GetPageAsync(int page, int pageSize, bool includeEmpty, CancellationToken ct = default);

    /// <summary>GET api/Runs/{runId}: one acquisition; null if unknown.</summary>
    Task<RunHistoryLine?> GetRunAsync(long runId, CancellationToken ct = default);

    /// <summary>GET api/Runs/{runId}/job-links: its job links; null if unknown.</summary>
    Task<RunJobLinks?> GetJobLinksAsync(long runId, CancellationToken ct = default);

    /// <summary>GET api/Runs/listings/{listingId}/job-links: the job links of a listing without a journal; null if unknown.</summary>
    Task<RunJobLinks?> GetListingJobLinksAsync(string listingId, CancellationToken ct = default);
}
