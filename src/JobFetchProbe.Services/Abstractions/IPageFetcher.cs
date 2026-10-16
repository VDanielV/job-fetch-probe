using JobFetchProbe.Models.Jobs;


namespace JobFetchProbe.Services.Abstractions;

/// <summary>Downloads a page. Implemented in Infrastructure with a factory-managed HttpClient.</summary>
public interface IPageFetcher
{
    Task<FetchedPage> GetAsync(Uri url, CancellationToken ct = default);
}
