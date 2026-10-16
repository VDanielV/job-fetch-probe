namespace JobFetchProbe.Models.Jobs;

/// <summary>A job read from its page: what step 2 stores in the database.</summary>
public sealed record JobPosting
{
    public required string JobId { get; init; }

    /// <summary>Where the job can be reached. Always an absolute http(s) address.</summary>
    public required Uri SourceUrl { get; init; }

    public string? Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public string? Posted { get; init; }
    public string? EmploymentType { get; init; }

    /// <summary>Extra labelled facts shown on the page, e.g. "Tipo de empleo: Jornada completa".</summary>
    public IReadOnlyList<JobDetail> Details { get; init; } = [];

    public string? Description { get; init; }

    /// <summary>Which page layout the data was read from.</summary>
    public string ParsedFrom { get; init; } = string.Empty;

    public DateTimeOffset RetrievedAt { get; init; }
}
