using JobFetchProbe.Models.Catalog;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The two calls the web app makes to the API's JobCatalog controller. The web app reads jobs only through these.
/// Both throw <see cref="ApiUnavailableException"/> when the jobs cannot be shown (no database yet, or no API).
/// </summary>
public interface IJobCatalogClient
{
    /// <summary>GET api/JobCatalog?page=&amp;pageSize=: one page of jobs, newest first. A page past the end returns the last page.</summary>
    Task<JobSummaryPage> GetPageAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>GET api/JobCatalog/{jobId}: one job in full, or null if the API answers 404.</summary>
    Task<StoredJob?> GetJobAsync(string jobId, CancellationToken ct = default);
}
