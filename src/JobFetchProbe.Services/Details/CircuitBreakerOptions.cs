namespace JobFetchProbe.Services.Details;

/// <param name="FailureThreshold">Consecutive network failures or server errors (5xx) that open the circuit. A refusal opens it at once.</param>
/// <param name="Cooldown">Wait after the first opening; doubled after each further opening.</param>
/// <param name="MaxCooldown">Upper limit for the doubled wait.</param>
/// <param name="StopAfterOpenings">When the circuit opens this many times in one run, the run stops instead of waiting again. 1 = stop at the first opening.</param>
public sealed record CircuitBreakerOptions(int FailureThreshold, TimeSpan Cooldown, TimeSpan MaxCooldown, int StopAfterOpenings)
{
    public static CircuitBreakerOptions Default { get; } = new(3, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), 3);
}
