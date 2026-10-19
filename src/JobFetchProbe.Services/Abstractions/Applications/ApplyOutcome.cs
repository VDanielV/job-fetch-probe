namespace JobFetchProbe.Services.Abstractions.Applications;

/// <summary>What happened to "I applied".</summary>
public enum ApplyOutcome
{
    Added,
    NotFound,
    AlreadyApplied,
    Archived,
}
