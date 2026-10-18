namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>Bound from "FetchJobDetails:CircuitBreaker".</summary>
public sealed class CircuitBreakerSettings
{
    /// <summary>Network failures or server errors in a row that open the circuit. A refusal opens it at once.</summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>Wait after the first opening, in seconds (300 = 5 minutes). Doubled after each further opening.</summary>
    public int CooldownSeconds { get; set; } = 300;

    /// <summary>Upper limit for the doubled wait, in seconds (1800 = 30 minutes).</summary>
    public int MaxCooldownSeconds { get; set; } = 1800;

    /// <summary>When the circuit opens this many times in one run, the run stops. 1 = stop at the first opening.</summary>
    public int StopAfterOpenings { get; set; } = 3;

    public CircuitBreakerOptions ToOptions() => new(
        FailureThreshold,
        TimeSpan.FromSeconds(CooldownSeconds),
        TimeSpan.FromSeconds(Math.Max(MaxCooldownSeconds, CooldownSeconds)),
        StopAfterOpenings);
}
