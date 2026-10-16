namespace JobFetchProbe.Data.Entities;

/// <summary>One row per job, whichever listings or searches it appears in.</summary>
public sealed class JobRow
{
    public string JobId { get; set; } = string.Empty;

    /// <summary>The job's page: from the job page once read, else from the first card that listed it.</summary>
    public string Url { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Posted { get; set; }
    public string? EmploymentType { get; set; }
    public string? Description { get; set; }
    public string? ParsedFrom { get; set; }

    /// <summary>When the job page was read. Null until it is.</summary>
    public DateTimeOffset? RetrievedAt { get; set; }

    /// <summary>See <see cref="JobStatus"/>.</summary>
    public string Status { get; set; } = JobStatus.Pending;

    /// <summary>Why the last attempt to read the job page failed; null after a successful read.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>When the job was first seen in a listing.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }

    public List<JobDetailRow> Details { get; set; } = [];
}
