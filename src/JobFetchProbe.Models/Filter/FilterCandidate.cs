using JobFetchProbe.Models.Catalog;

namespace JobFetchProbe.Models.Filter;

/// <summary>A listed job with its description, for the text filter to look at.</summary>
public sealed record FilterCandidate(JobSummary Summary, string? Description);
