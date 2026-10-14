namespace JobFetchProbe.Models;

public enum JobResultStatus
{
    /// <summary>The job was read and saved.</summary>
    Saved,

    /// <summary>The site refused the request or asked for a sign-in.</summary>
    Blocked,

    /// <summary>Bad input, transport failure or unexpected HTTP status.</summary>
    Failed,
}

/// <summary>Outcome of reading one link or file.</summary>
public sealed record JobResult(JobResultStatus Status, JobPosting? Job, string? SavedTo, string? Message)
{
    public static JobResult Saved(JobPosting job, string path) => new(JobResultStatus.Saved, job, path, null);
    public static JobResult Blocked(string message) => new(JobResultStatus.Blocked, null, null, message);
    public static JobResult Failed(string message) => new(JobResultStatus.Failed, null, null, message);
}
