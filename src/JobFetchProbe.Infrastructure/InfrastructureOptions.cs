namespace JobFetchProbe.Infrastructure;

public sealed class InfrastructureOptions
{
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 30;

    /// <summary>Directory for probe-report.json and probe-log.jsonl.</summary>
    public string OutDir { get; init; } = "out";

    public int TimeoutSeconds { get; init; } = 15;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutDir))
        {
            throw new ArgumentException("output directory is required");
        }
        if (TimeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
        {
            throw new ArgumentException($"timeout must be {MinTimeoutSeconds} to {MaxTimeoutSeconds} seconds");
        }
    }
}
