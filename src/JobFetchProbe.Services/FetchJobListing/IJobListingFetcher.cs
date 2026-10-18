using JobFetchProbe.Models.FetchJobListing;

namespace JobFetchProbe.Services.FetchJobListing;

/// <summary>Step 1, FetchJobListing: reads every saved search page in the input folder, stores their job links in the database, and archives the pages read.</summary>
public interface IJobListingFetcher
{
    /// <param name="progress">Told before and after each page is read.</param>
    Task<JobListingFetchResult> FetchAsync(IProgress<JobListingFetchProgress>? progress = null, CancellationToken ct = default);
}
