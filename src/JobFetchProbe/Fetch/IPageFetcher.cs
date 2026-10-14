namespace JobFetchProbe.Fetch;

/// <summary>One redirect seen while answering a request. Only the host is kept, never the path.</summary>
public sealed record RedirectHop(int Status, string ToHost, bool Followed);

/// <summary>
/// Result of one logical GET. <see cref="Body"/> lives in memory only and is never written to disk.
/// </summary>
public sealed record FetchResult
{
    /// <summary>HTTP status of the last response, or 0 when the transport failed.</summary>
    public int Status { get; init; }
    public required Uri RequestedUrl { get; init; }
    public required Uri FinalUrl { get; init; }
    public string? ContentType { get; init; }
    public string Body { get; init; } = string.Empty;
    public int BodyLength { get; init; }
    public int RequestsSent { get; init; }
    public IReadOnlyList<RedirectHop> Hops { get; init; } = [];
    public string? TransportError { get; init; }
    public string? StopReason { get; init; }
}

public interface IPageFetcher
{
    /// <summary>Sends one GET (plus at most 3 in-scope redirects). Never retries.</summary>
    Task<FetchResult> GetAsync(Uri url, string accept, CancellationToken ct = default);
}
