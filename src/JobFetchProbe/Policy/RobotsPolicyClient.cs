using JobFetchProbe.Fetch;

namespace JobFetchProbe.Policy;

/// <summary>Phase 0: one GET of robots.txt through the shared fetcher.</summary>
public sealed class RobotsPolicyClient(IPageFetcher fetcher)
{
    public static readonly Uri RobotsUrl = new("https://www.linkedin.com/robots.txt");

    public async Task<(FetchResult Fetch, RobotsPolicyReport? Report)> ReadAsync(CancellationToken ct = default)
    {
        var result = await fetcher.GetAsync(RobotsUrl, "text/plain", ct);

        if (result.TransportError is null && result.Status == 200)
        {
            return (result, RobotsPolicyParser.Analyze(result.Body, result.Status));
        }

        return (result, null);
    }
}
