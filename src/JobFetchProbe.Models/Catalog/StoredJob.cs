using JobFetchProbe.Models.Archive;
using JobFetchProbe.Models.Jobs;

namespace JobFetchProbe.Models.Catalog;

/// <summary>Everything the database holds about one job, for its detail page.</summary>
public sealed record StoredJob
{
    /// <summary>The app's own ID of the job.</summary>
    public required long JobId { get; init; }

    /// <summary>Where the job comes from, e.g. linkedin (<see cref="Jobs.JobSources"/>).</summary>
    public required string Source { get; init; }

    /// <summary>The site's own ID, e.g. LinkedIn's 4476446382, or the Notion import's Notion-A027.</summary>
    public required string SourceJobId { get; init; }
    public required Uri Url { get; init; }
    public string? Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public string? Posted { get; init; }
    public string? EmploymentType { get; init; }
    public IReadOnlyList<JobDetail> Details { get; init; } = [];
    public string? Description { get; init; }

    /// <summary>Pending, Read, Blocked, Failed or Gone.</summary>
    public required string Status { get; init; }

    public string? LastError { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset? RetrievedAt { get; init; }
    public DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>When and why the job was archived; null for a job to review.</summary>
    public JobArchiveInfo? Archive { get; init; }
}
