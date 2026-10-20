using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>The job's fit, or why it could not be scored.</summary>
public sealed record JobReadFollowUpResult(JobFitResult? Fit, string? Failure);
