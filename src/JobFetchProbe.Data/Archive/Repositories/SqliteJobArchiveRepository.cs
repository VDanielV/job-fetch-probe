using JobFetchProbe.Data.Archive.Entities;
using JobFetchProbe.Models.Archive;
using JobFetchProbe.Services.Abstractions.Archive;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data.Archive.Repositories;

/// <summary>Records archived jobs in the ArchivedJobs table. Never changes Jobs.</summary>
public sealed class SqliteJobArchiveRepository(IDbContextFactory<JobsDbContext> contexts) : IJobArchiveRepository
{
    public async Task<ArchiveResult> ArchiveAsync(
        IReadOnlyList<string> jobIds, ArchiveReason reason, string? note, DateTimeOffset at, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var known = await db.Jobs.AsNoTracking().Where(j => jobIds.Contains(j.JobId)).Select(j => j.JobId).ToListAsync(ct);
        var already = await db.ArchivedJobs.AsNoTracking().Where(a => jobIds.Contains(a.JobId)).Select(a => a.JobId).ToListAsync(ct);

        var toArchive = known.Except(already, StringComparer.Ordinal).ToList();
        foreach (var jobId in toArchive)
        {
            db.ArchivedJobs.Add(new ArchivedJobRow { JobId = jobId, ArchivedAt = at, Reason = reason.ToString(), Note = note });
        }

        // One SaveChanges: all the rows are added in one transaction, or none.
        await db.SaveChangesAsync(ct);
        return new ArchiveResult(toArchive.Count, already.Count, jobIds.Count - known.Count);
    }
}
