using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>The job step's storage: one row per job in Jobs. Reading the same job again overwrites it.</summary>
public sealed class SqliteJobRepository(IDbContextFactory<JobsDbContext> contexts, SqliteLocations locations) : IJobRepository
{
    public async Task<string> SaveAsync(JobPosting job, CancellationToken ct = default)
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
        return locations.Job(job.JobId);
    }
}
