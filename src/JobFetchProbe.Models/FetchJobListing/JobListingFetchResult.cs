namespace JobFetchProbe.Models.FetchJobListing;

/// <summary>Outcome of one run of step 1, FetchJobListing.</summary>
/// <param name="Batch">The merged jobs, or null when no page was read successfully.</param>
/// <param name="ListingId">The listing's ID in the database, or null when nothing was stored.</param>
public sealed record JobListingFetchResult(
    IReadOnlyList<JobListingFileResult> Files,
    JobSearchBatch? Batch,
    string? ListingId)
{
    public static JobListingFetchResult Empty { get; } = new([], null, null);
}
