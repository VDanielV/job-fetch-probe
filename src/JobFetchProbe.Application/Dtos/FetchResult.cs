namespace JobFetchProbe.Application.Dtos;

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
