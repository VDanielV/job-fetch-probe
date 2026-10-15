namespace JobFetchProbe.Models;

public enum JobResultStatus
{
    /// <summary>The job was read and saved.</summary>
    Saved,

    /// <summary>The site refused the request or asked for a sign-in.</summary>
    Blocked,

    /// <summary>Bad input, transport failure or unexpected HTTP status.</summary>
    Failed,

    /// <summary>Read successfully but not stored on its own (the inbox step stores it as part of a batch).</summary>
    Read,
}

/// <summary>Outcome of reading one link or file.</summary>
public sealed record JobResult(JobResultStatus Status, JobPosting? Job, string? SavedTo, string? Message)
{
    /// <summary>HTTP status of the page, when it was fetched from a link (0 = no response). Null for saved files.</summary>
    public int? HttpStatus { get; init; }

    public static JobResult Saved(JobPosting job, string path) => new(JobResultStatus.Saved, job, path, null);
    public static JobResult Blocked(string message) => new(JobResultStatus.Blocked, null, null, message);
    public static JobResult Failed(string message) => new(JobResultStatus.Failed, null, null, message);
    public static JobResult Read(JobPosting job) => new(JobResultStatus.Read, job, null, null);
}
