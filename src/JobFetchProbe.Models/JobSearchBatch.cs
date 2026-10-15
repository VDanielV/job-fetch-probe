namespace JobFetchProbe.Models;

/// <summary>
/// All the jobs read from the inbox in one run, merged and without duplicates.
/// Saved as &lt;BatchId&gt;.json, where BatchId is the run's local time as yyyyMMdd_HHmmss.
/// </summary>
public sealed record JobSearchBatch
{
    public required string BatchId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>One entry per inbox file that was read successfully.</summary>
    public IReadOnlyList<BatchSource> Sources { get; init; } = [];

    /// <summary>Number of unique jobs in <see cref="Jobs"/>.</summary>
    public int Count => Jobs.Count;

    /// <summary>Unique by JobId; the first file that lists a job wins.</summary>
    public IReadOnlyList<JobListing> Jobs { get; init; } = [];
}

/// <summary>One inbox file that contributed to a batch.</summary>
/// <param name="File">File name as it was in the inbox.</param>
/// <param name="SourceUrl">The search page's address (canonical link, og:url, or the browser's "saved from" note).</param>
/// <param name="Jobs">Jobs found in this file.</param>
/// <param name="NewJobs">Jobs from this file not already found in an earlier file of the same run.</param>
public sealed record BatchSource(string File, Uri SourceUrl, string? Query, int? TotalResults, int Jobs, int NewJobs);
