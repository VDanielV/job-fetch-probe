namespace JobFetchProbe.Models.FetchJobListing;

/// <summary>What happened to one saved page in the input folder.</summary>
/// <param name="Success">Read with at least one job. Only successful files are archived.</param>
/// <param name="ArchivedTo">Where the file was moved, or null if it stayed in the input folder.</param>
/// <param name="Message">Why the file failed, or why it could not be archived.</param>
public sealed record JobListingFileResult(
    string File,
    bool Success,
    int Jobs,
    int NewJobs,
    string? ArchivedTo,
    string? Message);
