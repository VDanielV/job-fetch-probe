using JobFetchProbe.Models.Search;

namespace JobFetchProbe.Services.FetchJobListing;

/// <summary>Reads the job links from a search results page saved from the browser.</summary>
public interface ISearchService
{
    /// <summary>
    /// Reads a saved search page. Returns status Read with the jobs on success, otherwise Blocked or Failed with
    /// a message. Stores nothing: step 1 stores the jobs of all pages together.
    /// </summary>
    Task<SearchResult> ReadFileAsync(string path, CancellationToken ct = default);
}
