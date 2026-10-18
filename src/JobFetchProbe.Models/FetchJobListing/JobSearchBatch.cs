using JobFetchProbe.Models.Search;

namespace JobFetchProbe.Models.FetchJobListing;

/// <summary>
/// All the jobs read from the input folder in one run, merged and without duplicates.
/// Stored as one listing in the database, where BatchId is the run's local time as yyyyMMdd_HHmmss.
/// </summary>
public sealed record JobSearchBatch
{
    public required string BatchId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>One entry per saved page that was read successfully.</summary>
    public IReadOnlyList<BatchSource> Sources { get; init; } = [];

    /// <summary>Number of unique jobs in <see cref="Jobs"/>.</summary>
    public int Count => Jobs.Count;

    /// <summary>Unique by JobId; the first file that lists a job wins.</summary>
    public IReadOnlyList<JobListing> Jobs { get; init; } = [];
}
