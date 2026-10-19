namespace JobFetchProbe.Models.Fit;

/// <summary>Every stored assessment of a job, newest first.</summary>
/// <param name="CurrentProfileVersion">The hash of the Fit files as they are now, to mark older assessments; null when they cannot be read.</param>
public sealed record JobFitHistory(IReadOnlyList<JobFitAssessment> Assessments, string? CurrentProfileVersion);
