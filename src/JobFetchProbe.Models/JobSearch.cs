namespace JobFetchProbe.Models;

/// <summary>The job links found on one search results page. Saved as &lt;SearchId&gt;.json.</summary>
public sealed record JobSearch
{
    /// <summary>File-name-safe name of the search, e.g. "net-empleos-valencia".</summary>
    public required string SearchId { get; init; }

    /// <summary>Where the search page can be reached. Always an absolute http(s) address.</summary>
    public required Uri SourceUrl { get; init; }

    /// <summary>The search as the page describes it, e.g. ".net Jobs in Valencia".</summary>
    public string? Query { get; init; }

    /// <summary>Total results the site reports for the search; usually more than the page shows.</summary>
    public int? TotalResults { get; init; }

    /// <summary>Number of unique jobs in <see cref="Jobs"/>.</summary>
    public int Count => Jobs.Count;

    public IReadOnlyList<JobListing> Jobs { get; init; } = [];

    /// <summary>Which page layout the links were read from.</summary>
    public string ParsedFrom { get; init; } = string.Empty;

    public DateTimeOffset RetrievedAt { get; init; }
}
