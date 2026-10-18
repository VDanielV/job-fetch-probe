namespace JobFetchProbe.Models.Filter;

/// <summary>Every listed job (newest job IDs first) for the text filter, and how many jobs are archived.</summary>
public sealed record FilterCandidates(IReadOnlyList<FilterCandidate> Jobs, int ArchivedCount);
