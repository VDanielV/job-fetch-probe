using JobFetchProbe.Models.Jobs;

namespace JobFetchProbe.Services;

/// <summary>Reads one job page from its link.</summary>
public interface IJobService
{
    /// <summary>
    /// Fetches and reads one job page. Returns status Read with the job on success, otherwise Blocked or Failed
    /// with a message and the HTTP status. Stores nothing: step 2 decides what to store.
    /// </summary>
    Task<JobResult> ReadUrlAsync(Uri url, CancellationToken ct = default);
}
