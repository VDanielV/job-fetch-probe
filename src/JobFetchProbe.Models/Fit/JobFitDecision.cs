namespace JobFetchProbe.Models.Fit;

/// <summary>What to do with one stored job after it has been scored.</summary>
public enum JobFitDecision
{
    Apply,
    Maybe,
    Skip,
}
