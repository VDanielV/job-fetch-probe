using JobFetchProbe.Application.Dtos;

namespace JobFetchProbe.Application.Abstractions;

/// <summary>The only way a use case reaches the network. Implemented in Infrastructure.</summary>
public interface IPageFetcher
{
    /// <summary>Sends one GET (plus at most 3 in-scope redirects). Never retries.</summary>
    Task<FetchResult> GetAsync(Uri url, string accept, CancellationToken ct = default);
}
