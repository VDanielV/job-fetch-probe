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

    public const int MinFitFailuresBeforeOff = 1;
    public const int MaxFitFailuresBeforeOff = 10;

    /// <summary>Score each job's fit right after its page is read; the scoring time adds to the pause.</summary>
    public bool AssessFit { get; set; } = true;

    /// <summary>Failed scores in a row after which scoring is off for the rest of the run (Ollama is most likely down).</summary>
    public int FitFailuresBeforeOff { get; set; } = 3;
}
