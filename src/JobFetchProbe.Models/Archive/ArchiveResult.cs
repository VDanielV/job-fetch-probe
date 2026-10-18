namespace JobFetchProbe.Models.Archive;

/// <summary>What an archive request did.</summary>
/// <param name="Archived">Jobs archived now.</param>
/// <param name="AlreadyArchived">Jobs that were archived before; they keep their first reason and date.</param>
/// <param name="NotFound">IDs that are not in the database.</param>
public sealed record ArchiveResult(int Archived, int AlreadyArchived, int NotFound);
