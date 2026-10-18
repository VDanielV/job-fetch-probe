using JobFetchProbe.Models.FetchJobListing;

namespace JobFetchProbe.Services.Journal;

/// <summary>
/// Records one step 1 run in the journal. Pass it as (or next to) the fetcher's progress; then call
/// <see cref="CompleteAsync"/> with the result, or <see cref="FailAsync"/> with the error. Recording never throws and
/// never slows the fetch: writes happen in the background, in order.
/// </summary>
public interface IJobListingRunRecorder : IProgress<JobListingFetchProgress>, IAsyncDisposable
{
    /// <summary>The run's id in the journal; 0 when the journal could not be started (the fetch runs anyway).</summary>
    long RunId { get; }

    /// <summary>Records the result (Completed) and waits until everything is written.</summary>
    Task CompleteAsync(JobListingFetchResult result);

    /// <summary>Records the error (Failed) and waits until everything is written.</summary>
    Task FailAsync(Exception error);
}
