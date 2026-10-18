using JobFetchProbe.Models.Acquisition;

namespace JobFetchProbe.Services.Acquisition;

/// <summary>
/// One acquisition: step 1 (FetchJobListing: saved search pages → job listing), then step 2 (FetchJobDetails: every
/// job without a description → its description), each recorded in the journal.
/// </summary>
public interface IAcquisitionService
{
    /// <summary>
    /// True when an acquisition is running somewhere: a step's run is still Running in the journal and shows signs of
    /// life, whichever process started it. Two acquisitions at once would double the requests to the site.
    /// </summary>
    Task<bool> IsRunningAsync(CancellationToken ct = default);

    /// <param name="observer">Told about each step as it goes.</param>
    /// <param name="ct">Stops step 2 after the current job; what was read so far is kept.</param>
    Task<AcquisitionResult> RunAsync(IAcquisitionObserver observer, CancellationToken ct = default);
}
