using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.FetchJobListing;

namespace JobFetchProbe.Services.Acquisition;

/// <summary>
/// Told what an acquisition is doing, in order: step 1 starting, its journal run, each page, its result; then the
/// same for step 2. Called on the thread that runs the acquisition, so whatever it writes comes out in order.
/// The API writes it to its log (<see cref="LoggingAcquisitionObserver"/>).
/// </summary>
public interface IAcquisitionObserver
{
    /// <summary>Step 1 (FetchJobListing) is about to start.</summary>
    void JobListingStarting();

    /// <summary>Step 2 (FetchJobDetails) is about to start.</summary>
    void JobDetailsStarting();

    /// <summary>The current step is being recorded as this journal run; 0 when the journal could not be started (the step runs anyway).</summary>
    void JournalRunStarted(long runId);

    /// <summary>Step 1 read (or is reading) one saved page.</summary>
    void JobListingProgress(JobListingFetchProgress progress);

    /// <summary>Step 1 finished and its journal run is written.</summary>
    void JobListingFinished(JobListingFetchResult result);

    /// <summary>Step 2 started, read a job, changed the circuit, is waiting, or stopped.</summary>
    void JobDetailsEvent(JobDetailsFetchEvent e);

    /// <summary>Step 2 finished and its journal run is written.</summary>
    void JobDetailsFinished(JobDetailsFetchResult result);
}
