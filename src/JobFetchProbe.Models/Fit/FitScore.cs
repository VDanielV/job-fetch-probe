namespace JobFetchProbe.Models.Fit;

/// <summary>The latest fit assessment of a job, as the lists show it.</summary>
public sealed record FitScore(int Fit, JobFitDecision Decision, bool HardSkip, DateTimeOffset AssessedAt);
