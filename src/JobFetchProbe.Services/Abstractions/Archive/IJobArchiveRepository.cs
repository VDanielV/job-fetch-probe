using JobFetchProbe.Models.Archive;

namespace JobFetchProbe.Services.Abstractions.Archive;

/// <summary>Where archived jobs are recorded. Implemented by the data layer.</summary>
public interface IJobArchiveRepository
{
    /// <summary>
    /// Marks the jobs as archived, all in one transaction. A job already archived keeps its first reason and date;
    /// an ID that is not in the database is skipped. The counts say which was which.
    /// </summary>
    /// <param name="jobIds">Distinct, trimmed job IDs.</param>
    Task<ArchiveResult> ArchiveAsync(IReadOnlyList<string> jobIds, ArchiveReason reason, string? note, DateTimeOffset at, CancellationToken ct = default);
}
