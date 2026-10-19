using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Models.Filter;

namespace JobFetchProbe.Services.Catalog;

/// <summary>
/// Reads the stored jobs for the API: one page of the list, or one job in full. Read-only.
/// Throws <see cref="JobStoreUnavailableException"/> while the database does not exist yet.
/// </summary>
public interface IJobRetriever
{
    /// <summary>
    /// One page of jobs, newest job IDs first, or the best latest fit first with <paramref name="sort"/> Fit. Archived jobs
    /// are left out unless <paramref name="includeArchived"/>.
    /// </summary>
    /// <param name="page">1-based. A page past the end returns the last page; the result says which page it is.</param>
    /// <param name="pageSize">Jobs per page, 1 to <see cref="JobRetriever.MaxPageSize"/>.</param>
    /// <param name="query">A text filter (words, -word, "a phrase"); null or blank for none.</param>
    /// <param name="scope">Where the filter looks: the title and description, or the title only.</param>
    /// <param name="places">City chips to list ("valencia", "spain", "other"); none for every place.</param>
    /// <param name="modes">Work modes to list ("remote", "hybrid", "onsite", "notstated"); none for every mode.</param>
    /// <param name="sort">Newest first, or best fit first (jobs never assessed last, ties newest first).</param>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1, or the page size is outside the limits.</exception>
    Task<JobSummaryPage> GetPageAsync(
        int page, int pageSize, bool includeArchived = false, string? query = null, FilterScope scope = FilterScope.All,
        IReadOnlyList<string>? places = null, IReadOnlyList<string>? modes = null, JobSort sort = JobSort.Newest,
        CancellationToken ct = default);

    /// <summary>One job with its details and description, or null if there is no such job.</summary>
    Task<StoredJob?> GetJobAsync(string jobId, CancellationToken ct = default);
}
