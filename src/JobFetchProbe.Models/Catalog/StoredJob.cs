using JobFetchProbe.Models.Jobs;

namespace JobFetchProbe.Models.Catalog;

/// <summary>Everything the database holds about one job, for its detail page.</summary>
public sealed record StoredJob
{
    public required string JobId { get; init; }
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
}
