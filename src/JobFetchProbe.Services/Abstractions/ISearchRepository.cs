using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Stores search results. Implemented in Infrastructure as one JSON file per search.</summary>
public interface ISearchRepository
{
    /// <returns>Where the search was stored (the full file path).</returns>
    Task<string> SaveAsync(JobSearch search, CancellationToken ct = default);
}
