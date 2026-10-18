namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>Step 2's rules, bound from the "FetchJobDetails" section of appsettings.json.</summary>
public sealed class FetchJobDetailsSettings
{
    public const string SectionName = "FetchJobDetails";

    public const int MinDelaySeconds = 1;
    public const int MaxDelaySeconds = 120;

    /// <summary>Pause between two job requests. Keeps the app from hammering the site.</summary>
    public int DelaySeconds { get; set; } = 5;

    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();
}
