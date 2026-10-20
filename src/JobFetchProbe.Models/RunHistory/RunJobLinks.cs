namespace JobFetchProbe.Models.RunHistory;

/// <summary>Every job link of a run's listing, in the order step 1 found them.</summary>
public sealed record RunJobLinks(RunHistoryLine Run, IReadOnlyList<RunJobLink> Links);
