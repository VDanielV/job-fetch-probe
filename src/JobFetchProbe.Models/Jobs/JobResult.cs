namespace JobFetchProbe.Models.Jobs;

/// <summary>Outcome of reading one job page.</summary>
public sealed record JobResult(JobResultStatus Status, JobPosting? Job, string? Message)
{
    /// <summary>HTTP status of the page (0 = no response).</summary>
    public int? HttpStatus { get; init; }

    public static JobResult Read(JobPosting job) => new(JobResultStatus.Read, job, null);
    public static JobResult Blocked(string message) => new(JobResultStatus.Blocked, null, message);
    public static JobResult Failed(string message) => new(JobResultStatus.Failed, null, message);
}
