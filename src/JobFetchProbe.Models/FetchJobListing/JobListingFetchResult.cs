namespace JobFetchProbe.Models.FetchJobListing;

/// <summary>Outcome of one run of step 1, FetchJobListing.</summary>
/// <param name="Batch">The merged jobs, or null when no page was read successfully.</param>
/// <param name="ListingId">The listing's ID in the database, or null when nothing was stored.</param>
/// <param name="NewJobs">Of the batch's jobs, those new to the database; the others were already known from earlier runs.</param>
/// <param name="Stopped">Stopped before the end: nothing was stored and no page was archived; the pages stay in the input folder.</param>
public sealed record JobListingFetchResult(
    IReadOnlyList<JobListingFileResult> Files,
    JobSearchBatch? Batch,
    string? ListingId,
    bool Stopped = false,
    int NewJobs = 0)
{
    public static JobListingFetchResult Empty { get; } = new([], null, null);
}
