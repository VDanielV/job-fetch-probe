using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Details;

/// <summary>Reads every job in a listing batch, one by one, and stores all their descriptions in one file.</summary>
public interface IDetailsService
{
    /// <param name="listingFile">Full path of the listing batch.</param>
    /// <param name="delay">Pause between two requests.</param>
    /// <param name="breaker">Circuit breaker settings for this run.</param>
    /// <param name="progress">Told about every job, every circuit change and, while waiting, the time left.</param>
    Task<DetailsRunResult> RunAsync(
        string listingFile,
        TimeSpan delay,
        CircuitBreakerOptions breaker,
        IProgress<DetailsEvent>? progress = null,
        CancellationToken ct = default);
}
