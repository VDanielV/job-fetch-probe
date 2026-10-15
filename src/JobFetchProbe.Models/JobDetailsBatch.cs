namespace JobFetchProbe.Models;

/// <summary>
/// The full job descriptions for one listing batch. Saved as &lt;BatchId&gt;.json in the details folder,
/// with the same BatchId as the listing it was built from. Rewritten after every job, so progress is never lost.
/// </summary>
public sealed record JobDetailsBatch
{
    /// <summary>Same as the listing batch, e.g. "20261014_183201".</summary>
    public required string BatchId { get; init; }

    /// <summary>Full path of the listing file the jobs came from.</summary>
    public required string ListingFile { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Number of jobs listed in the listing file.</summary>
    public int Listed { get; init; }

    /// <summary>Number of jobs in <see cref="Jobs"/>.</summary>
    public int Count => Jobs.Count;

    /// <summary>Listed jobs that are neither in <see cref="Jobs"/> nor in <see cref="Failures"/> yet.</summary>
    public int Pending { get; init; }

    /// <summary>Why the last run stopped before the end of the list, or null if it reached the end.</summary>
    public string? StoppedReason { get; init; }

    /// <summary>Jobs read successfully, in listing order.</summary>
    public IReadOnlyList<JobPosting> Jobs { get; init; } = [];

    /// <summary>Jobs that could not be read in the last run; they are tried again on the next run.</summary>
    public IReadOnlyList<JobFailure> Failures { get; init; } = [];
}

/// <param name="Status">Blocked or Failed.</param>
public sealed record JobFailure(string JobId, Uri Url, string Status, string Message);
