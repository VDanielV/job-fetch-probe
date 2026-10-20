namespace JobFetchProbe.Models.Jobs;

/// <summary>A job read from its page: what step 2 stores in the database.</summary>
public sealed record JobPosting
{
    /// <summary>Where the job comes from (<see cref="Jobs.JobSources"/>); LinkedIn unless set.</summary>
    public string Source { get; init; } = Jobs.JobSources.LinkedIn;

    /// <summary>The site's own ID of the job, e.g. LinkedIn's 4476446382.</summary>
    public required string SourceJobId { get; init; }

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
