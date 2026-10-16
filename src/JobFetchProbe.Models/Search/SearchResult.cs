using JobFetchProbe.Models.Jobs;

namespace JobFetchProbe.Models.Search;

/// <summary>Outcome of reading one saved search results page. Uses the same statuses as <see cref="JobResult"/>.</summary>
public sealed record SearchResult(JobResultStatus Status, JobSearch? Search, string? Message)
{
    public static SearchResult Read(JobSearch search) => new(JobResultStatus.Read, search, null);
    public static SearchResult Blocked(string message) => new(JobResultStatus.Blocked, null, message);
    public static SearchResult Failed(string message) => new(JobResultStatus.Failed, null, message);
}
