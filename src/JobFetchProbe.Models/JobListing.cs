namespace JobFetchProbe.Models;

/// <summary>One result card on a search results page.</summary>
public sealed record JobListing
{
    public required string JobId { get; init; }

    /// <summary>The job's page, without tracking parameters. Always an absolute http(s) address.</summary>
    public required Uri Url { get; init; }

    public string? Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }

    /// <summary>Listing date as given by the page (e.g. "2026-10-13"), or null.</summary>
    public string? ListedAt { get; init; }
}
