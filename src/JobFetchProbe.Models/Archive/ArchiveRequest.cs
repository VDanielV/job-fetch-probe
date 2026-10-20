namespace JobFetchProbe.Models.Archive;

/// <summary>Archive these jobs, all with the same reason and note.</summary>
/// <param name="JobIds">1 to 100 job IDs (the app's IDs).</param>
/// <param name="Note">Optional, at most 500 characters; required when the reason is <see cref="ArchiveReason.Other"/>.</param>
public sealed record ArchiveRequest(IReadOnlyList<long> JobIds, ArchiveReason Reason, string? Note = null);
