namespace JobFetchProbe.Models;

/// <summary>Outcome of reading one search results page. Uses the same statuses as <see cref="JobResult"/>.</summary>
public sealed record SearchResult(JobResultStatus Status, JobSearch? Search, string? SavedTo, string? Message)
{
    public static SearchResult Saved(JobSearch search, string path) => new(JobResultStatus.Saved, search, path, null);
    public static SearchResult Blocked(string message) => new(JobResultStatus.Blocked, null, null, message);
    public static SearchResult Failed(string message) => new(JobResultStatus.Failed, null, null, message);
    public static SearchResult Read(JobSearch search) => new(JobResultStatus.Read, search, null, null);
}
