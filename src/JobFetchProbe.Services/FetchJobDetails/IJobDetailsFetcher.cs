using JobFetchProbe.Models.FetchJobDetails;

namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>Step 2, FetchJobDetails: reads every job without a description, one by one, and stores each result in the database.</summary>
public interface IJobDetailsFetcher
{
    /// <summary>The pause between requests and the circuit breaker come from <see cref="FetchJobDetailsSettings"/>.</summary>
    /// <param name="progress">Told about every job, every circuit change and, while waiting, the time left.</param>
    Task<JobDetailsFetchResult> FetchAsync(IProgress<JobDetailsFetchEvent>? progress = null, CancellationToken ct = default);
}
