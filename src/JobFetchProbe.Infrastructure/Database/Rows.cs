namespace JobFetchProbe.Infrastructure.Database;

// Database rows. Kept separate from the Models records so the tables can change without touching the other layers.

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

    /// <summary>When the job was first seen in a listing, a search or the job step.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }

    public List<JobDetailRow> Details { get; set; } = [];
}

public static class JobStatus
{
    /// <summary>Listed, job page not read yet.</summary>
    public const string Pending = "Pending";

    /// <summary>Job page read; the description is stored.</summary>
    public const string Read = "Read";

    /// <summary>The site refused the last attempt.</summary>
    public const string Blocked = "Blocked";

    /// <summary>The last attempt failed (network, server error, not found, nothing readable).</summary>
    public const string Failed = "Failed";
}

/// <summary>A labelled fact from the job page, e.g. "Tipo de empleo: Jornada completa".</summary>
public sealed class JobDetailRow
{
    public int Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>One inbox run: the pages read and the jobs they listed.</summary>
public sealed class ListingRow
{
    /// <summary>yyyyMMdd_HHmmss, with _2, _3… if two runs share a second.</summary>
    public string BatchId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Last time the details step worked on this listing.</summary>
    public DateTimeOffset? DetailsUpdatedAt { get; set; }

    /// <summary>Jobs not tried yet when the details step last stopped.</summary>
    public int? DetailsPending { get; set; }

    /// <summary>Why the details step last stopped early; null if it reached the end.</summary>
    public string? DetailsStoppedReason { get; set; }

    public List<ListingSourceRow> Sources { get; set; } = [];
    public List<ListingJobRow> Jobs { get; set; } = [];
}

/// <summary>One saved page that contributed to a listing.</summary>
public sealed class ListingSourceRow
{
    public int Id { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string File { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? Query { get; set; }
    public int? TotalResults { get; set; }
    public int Jobs { get; set; }
    public int NewJobs { get; set; }
}

/// <summary>A job as its card showed it in one listing (the card may differ from the job page).</summary>
public sealed class ListingJobRow
{
    public string BatchId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? ListedAt { get; set; }
}

/// <summary>The search step's result for one search; replaced when the search is read again.</summary>
public sealed class SearchRow
{
    public string SearchId { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? Query { get; set; }
    public int? TotalResults { get; set; }
    public string ParsedFrom { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; set; }
    public List<SearchJobRow> Jobs { get; set; } = [];
}

/// <summary>A job as its card showed it in one search.</summary>
public sealed class SearchJobRow
{
    public string SearchId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? ListedAt { get; set; }
}
