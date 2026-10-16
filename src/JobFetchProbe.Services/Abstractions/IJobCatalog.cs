using JobFetchProbe.Models.Catalog;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Read-only view of the stored jobs, for the web app and <c>JobRetriever</c>. Implemented by the data layer.</summary>
public interface IJobCatalog
{
    /// <summary>False while the database does not exist yet (the console app creates it on its first run).</summary>
    bool IsAvailable { get; }

    /// <summary>One page of jobs, newest job IDs first.</summary>
    /// <param name="page">1-based; out-of-range values are clamped to the first or last page.</param>
    Task<JobSummaryPage> GetPageAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>One job with its details and description, or null if it is not in the database.</summary>
    Task<StoredJob?> GetAsync(string jobId, CancellationToken ct = default);
}
