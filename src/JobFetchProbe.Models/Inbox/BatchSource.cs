namespace JobFetchProbe.Models.Inbox;

/// <summary>One inbox file that contributed to a batch.</summary>
/// <param name="File">File name as it was in the inbox.</param>
/// <param name="SourceUrl">The search page's address (canonical link, og:url, or the browser's "saved from" note).</param>
/// <param name="Jobs">Jobs found in this file.</param>
/// <param name="NewJobs">Jobs from this file not already found in an earlier file of the same run.</param>
public sealed record BatchSource(string File, Uri SourceUrl, string? Query, int? TotalResults, int Jobs, int NewJobs);
