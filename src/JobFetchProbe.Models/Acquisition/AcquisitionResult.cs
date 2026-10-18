using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.FetchJobListing;

namespace JobFetchProbe.Models.Acquisition;

/// <summary>Outcome of one acquisition: step 1 (FetchJobListing), then step 2 (FetchJobDetails).</summary>
/// <param name="JobListingComplete">Every saved page in the input folder was read and archived (or there was none).</param>
/// <param name="NothingLeft">Nothing is left for a next run: <paramref name="JobListingComplete"/> and no job still without a description.</param>
public sealed record AcquisitionResult(
    JobListingFetchResult JobListing,
    JobDetailsFetchResult JobDetails,
    bool JobListingComplete,
    bool NothingLeft);
