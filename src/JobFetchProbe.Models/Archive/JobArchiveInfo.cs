namespace JobFetchProbe.Models.Archive;

/// <summary>When and why a job was archived.</summary>
public sealed record JobArchiveInfo(DateTimeOffset ArchivedAt, ArchiveReason Reason, string? Note);
