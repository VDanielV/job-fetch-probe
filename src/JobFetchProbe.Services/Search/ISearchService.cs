using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Search;

/// <summary>Reads the job links from one search results page (a link or a saved page) and stores them.</summary>
public interface ISearchService
{
    Task<SearchResult> FromUrlAsync(Uri url, CancellationToken ct = default);

    Task<SearchResult> FromFileAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Reads a saved search page without storing it. Returns status Read with the search on success,
    /// otherwise Blocked or Failed with a message. Used by the inbox step, which stores many pages as one batch.
    /// </summary>
    Task<SearchResult> ReadFileAsync(string path, CancellationToken ct = default);
}
