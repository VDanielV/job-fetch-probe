using JobFetchProbe.Models.Catalog;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Read-only view of the stored jobs, for the web app. Implemented with the database.</summary>
public interface IJobCatalog
{
    /// <summary>One page of jobs, newest job IDs first.</summary>
    /// <param name="page">1-based; out-of-range values are clamped to the first or last page.</param>
    Task<JobSummaryPage> GetPageAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>One job with its details and description, or null if it is not in the database.</summary>
    Task<StoredJob?> GetAsync(string jobId, CancellationToken ct = default);
}
