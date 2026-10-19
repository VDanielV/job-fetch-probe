namespace JobFetchProbe.Models.Fit;

/// <summary>One stored fit assessment of a job.</summary>
/// <param name="Model">The local model that answered, e.g. qwen2.5:7b.</param>
/// <param name="ProfileVersion">A short hash of the three Fit files the assessment was made with (never their content).</param>
public sealed record JobFitAssessment(
    long Id,
    DateTimeOffset AssessedAt,
    int Fit,
    JobFitDecision Decision,
    bool HardSkip,
    string Reason,
    IReadOnlyList<string> MissingInformation,
    string? Model,
    string ProfileVersion);
