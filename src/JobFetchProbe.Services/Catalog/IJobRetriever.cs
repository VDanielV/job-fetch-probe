using JobFetchProbe.Models.Catalog;

namespace JobFetchProbe.Services.Catalog;

/// <summary>
/// Reads the stored jobs for the API: one page of the list, or one job in full. Read-only.
/// Throws <see cref="JobStoreUnavailableException"/> while the database does not exist yet.
/// </summary>
public interface IJobRetriever
{
    /// <summary>One page of jobs, newest job IDs first.</summary>
    /// <param name="page">1-based. A page past the end returns the last page; the result says which page it is.</param>
    /// <param name="pageSize">Jobs per page, 1 to <see cref="JobRetriever.MaxPageSize"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1, or the page size is outside the limits.</exception>
    Task<JobSummaryPage> GetPageAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>One job with its details and description, or null if there is no such job.</summary>
    Task<StoredJob?> GetJobAsync(string jobId, CancellationToken ct = default);
}
