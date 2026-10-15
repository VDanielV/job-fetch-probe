namespace JobFetchProbe.Infrastructure.Details;

/// <summary>Bound from the "Details" section of appsettings.json.</summary>
public sealed class DetailsSettings
{
    public const string SectionName = "Details";

    public const int MinDelaySeconds = 1;
    public const int MaxDelaySeconds = 120;

    /// <summary>Folder for the &lt;BatchId&gt;.json files with the full job descriptions.</summary>
    public string OutputFolder { get; set; } = "job-listings";

    /// <summary>Pause between two job requests. Keeps the app from hammering the site.</summary>
    public int DelaySeconds { get; set; } = 5;

    /// <summary>After the inbox step writes a listing, read its jobs straight away.</summary>
    public bool RunAfterInbox { get; set; } = true;

    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();
}

/// <summary>Bound from "Details:CircuitBreaker".</summary>
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

    public JobFetchProbe.Services.Details.CircuitBreakerOptions ToOptions() => new(
        FailureThreshold,
        TimeSpan.FromSeconds(CooldownSeconds),
        TimeSpan.FromSeconds(Math.Max(MaxCooldownSeconds, CooldownSeconds)),
        StopAfterOpenings);
}
