using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Models.Filter;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The two calls the web app makes to the API's JobCatalog controller. The web app reads jobs only through these.
/// Both throw <see cref="ApiUnavailableException"/> when the jobs cannot be shown (no database yet, or no API).
/// </summary>
public interface IJobCatalogClient
{
    /// <summary>
    /// GET api/JobCatalog?page=&amp;pageSize=&amp;includeArchived=&amp;q=&amp;in=: one page of jobs, newest first; archived jobs only
    /// when asked for; only the matches of the text filter <paramref name="query"/> and of the city and work-mode chips
    /// (&amp;place=, &amp;mode=, repeatable) when there are any. A page past the end returns the last page.
    /// </summary>
    Task<JobSummaryPage> GetPageAsync(
        int page, int pageSize, bool includeArchived = false, string? query = null, FilterScope scope = FilterScope.All,
        IReadOnlyList<string>? places = null, IReadOnlyList<string>? modes = null, JobSort sort = JobSort.Newest,
        CancellationToken ct = default);

    /// <summary>GET api/JobCatalog/{jobId}: one job in full, or null if the API answers 404.</summary>
    Task<StoredJob?> GetJobAsync(string jobId, CancellationToken ct = default);
}
