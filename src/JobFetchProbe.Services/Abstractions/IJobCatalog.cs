using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Models.Filter;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Read-only view of the stored jobs, for the web app and <c>JobRetriever</c>. Implemented by the data layer.</summary>
public interface IJobCatalog
{
    /// <summary>False while the database does not exist yet (the API creates it when it starts).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// One page of jobs, most recently found first. Archived jobs are left out (and out of the count) unless asked for; jobs
    /// applied to are always left out.
    /// </summary>
    /// <param name="page">1-based; out-of-range values are clamped to the first or last page.</param>
    Task<JobSummaryPage> GetPageAsync(int page, int pageSize, bool includeArchived = false, CancellationToken ct = default);

    /// <summary>
    /// Every listed job with its description, most recently found first, for the text filter: the jobs to review, or all of
    /// them with <paramref name="includeArchived"/>. Jobs applied to are never among them.
    /// </summary>
    Task<FilterCandidates> GetFilterCandidatesAsync(bool includeArchived = false, CancellationToken ct = default);

    /// <summary>One job with its details, description and archive information, or null if it is not in the database.</summary>
    Task<StoredJob?> GetAsync(long jobId, CancellationToken ct = default);
}
