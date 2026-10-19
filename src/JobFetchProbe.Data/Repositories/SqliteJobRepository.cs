using JobFetchProbe.Data.Entities;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Search;
using JobFetchProbe.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data.Repositories;

/// <summary>
/// Step 2's storage: the Jobs table. Each call opens its own short-lived context and saves at once, so every job's
/// result is kept as soon as it is known.
/// </summary>
public sealed class SqliteJobRepository(IDbContextFactory<JobsDbContext> contexts, TimeProvider time) : IJobRepository
{
    public async Task<IReadOnlyList<JobListing>> GetUnreadAsync(CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var rows = await db.Jobs.AsNoTracking()
            .Where(j => JobStatus.ToRead.Contains(j.Status))
            .Where(j => !db.ArchivedJobs.Any(a => a.JobId == j.JobId)) // archived: never requested again
            .Where(j => !db.Applications.Any(a => a.JobId == j.JobId)) // applied to: never requested again
            .ToListAsync(ct);

        // Sorted here: SQLite cannot order by DateTimeOffset. Never-tried jobs first, then oldest first.
        return rows
            .OrderBy(j => j.Status == JobStatus.Pending ? 0 : 1)
            .ThenBy(j => j.FirstSeenAt)
            .ThenBy(j => j.JobId, StringComparer.Ordinal)
            .Select(j => j.ToCard())
            .ToList();
    }

    public async Task SaveDescriptionAsync(JobPosting job, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Jobs.Include(j => j.Details).SingleOrDefaultAsync(j => j.JobId == job.JobId, ct);
        if (row is null)
        {
            row = new JobRow { JobId = job.JobId, FirstSeenAt = job.RetrievedAt };
            db.Jobs.Add(row);
        }
        row.ApplyPosting(job);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveFailureAsync(string jobId, JobResultStatus status, bool gone, string message, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Jobs.SingleOrDefaultAsync(j => j.JobId == jobId, ct);
        if (row is null || row.Status == JobStatus.Read)
        {
            return; // never replace a stored description with a later failure
        }
        row.Status = gone ? JobStatus.Gone : status == JobResultStatus.Blocked ? JobStatus.Blocked : JobStatus.Failed;
        row.LastError = message;
        row.LastAttemptAt = time.GetLocalNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CountReadAsync(CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Jobs.CountAsync(j => j.Status == JobStatus.Read, ct);
    }
}
