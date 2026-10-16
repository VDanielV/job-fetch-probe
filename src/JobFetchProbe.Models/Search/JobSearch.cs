namespace JobFetchProbe.Models.Search;

/// <summary>The job links found on one saved search results page.</summary>
public sealed record JobSearch
{
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
