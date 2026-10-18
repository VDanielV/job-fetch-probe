using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The calls the web app makes to the API's Journal controller, to show an acquisition's progress.
/// They throw <see cref="ApiUnavailableException"/> when the API is not reachable.
/// </summary>
public interface IJournalClient
{
    /// <summary>GET api/Journal/runs?take=: the newest runs of both steps, newest first; empty while there is no journal yet.</summary>
    Task<IReadOnlyList<JournalRunSummary>> GetRecentRunsAsync(int take, CancellationToken ct = default);

    /// <summary>GET api/Journal/job-listing/runs/{runId}: a step 1 run with all its entries; null if unknown.</summary>
    Task<JobListingJournalRun?> GetJobListingRunAsync(long runId, CancellationToken ct = default);

    /// <summary>GET api/Journal/job-details/runs/{runId}: a step 2 run with all its entries; null if unknown.</summary>
    Task<JobDetailsJournalRun?> GetJobDetailsRunAsync(long runId, CancellationToken ct = default);
}
