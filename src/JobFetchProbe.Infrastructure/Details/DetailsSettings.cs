namespace JobFetchProbe.Infrastructure.Details;

/// <summary>Bound from the "Details" section of appsettings.json (step 2).</summary>
public sealed class DetailsSettings
{
    public const string SectionName = "Details";

    public const int MinDelaySeconds = 1;
    public const int MaxDelaySeconds = 120;

    /// <summary>Pause between two job requests. Keeps the app from hammering the site.</summary>
    public int DelaySeconds { get; set; } = 5;

    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();
}
