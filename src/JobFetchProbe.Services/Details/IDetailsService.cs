using JobFetchProbe.Models.Details;

namespace JobFetchProbe.Services.Details;

/// <summary>Step 2: reads every job without a description, one by one, and stores each result in the database.</summary>
public interface IDetailsService
{
    /// <param name="delay">Pause between two requests.</param>
    /// <param name="breaker">Circuit breaker settings for this run.</param>
    /// <param name="progress">Told about every job, every circuit change and, while waiting, the time left.</param>
    Task<DetailsRunResult> RunAsync(
        TimeSpan delay,
        CircuitBreakerOptions breaker,
        IProgress<DetailsEvent>? progress = null,
        CancellationToken ct = default);
}
