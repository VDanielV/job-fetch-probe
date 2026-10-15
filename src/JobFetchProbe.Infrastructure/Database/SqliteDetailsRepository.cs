using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>
/// The details step's storage. Descriptions go into the job's own row in Jobs, so a job already read — in this
/// listing, another listing or the job step — counts as done and is not requested again.
/// </summary>
public sealed class SqliteDetailsRepository(IDbContextFactory<JobsDbContext> contexts, SqliteLocations locations) : IDetailsRepository
{
    public async Task<JobDetailsBatch?> LoadAsync(string batchId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var listing = await db.Listings.AsNoTracking().SingleOrDefaultAsync(l => l.BatchId == batchId, ct);
        if (listing is null)
        {
            return null;
        }

        var listed = db.ListingJobs.Where(lj => lj.BatchId == batchId).Select(lj => lj.JobId);
        var jobs = await db.Jobs.AsNoTracking()
            .Include(j => j.Details)
            .Where(j => listed.Contains(j.JobId))
            .ToListAsync(ct);

        return new JobDetailsBatch
        {
            BatchId = batchId,
            ListingFile = locations.Listing(batchId),
            UpdatedAt = listing.DetailsUpdatedAt ?? listing.CreatedAt,
            Listed = await listed.CountAsync(ct),
            Pending = listing.DetailsPending ?? 0,
            StoppedReason = listing.DetailsStoppedReason,
            Jobs = jobs.Where(j => j.Status == JobStatus.Read).Select(j => j.ToPosting()).ToList(),
            Failures = jobs.Where(j => j.Status is JobStatus.Blocked or JobStatus.Failed)
                .Select(j => new JobFailure(j.JobId, new Uri(j.Url), j.Status, j.LastError ?? string.Empty))
                .ToList(),
        };
    }

    /// <summary>
    /// Called after every job: writes the jobs read since the last call, marks the failures, and records where the
    /// listing stands. Jobs already stored with the same read time are left untouched, so each call writes only what changed.
    /// </summary>
    public async Task<string> SaveAsync(JobDetailsBatch details, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var ids = details.Jobs.Select(j => j.JobId).Concat(details.Failures.Select(f => f.JobId)).Distinct(StringComparer.Ordinal).ToList();
        var rows = await db.Jobs.Include(j => j.Details).Where(j => ids.Contains(j.JobId)).ToDictionaryAsync(j => j.JobId, StringComparer.Ordinal, ct);

        foreach (var job in details.Jobs)
        {
            if (!rows.TryGetValue(job.JobId, out var row))
            {
                row = new JobRow { JobId = job.JobId, FirstSeenAt = job.RetrievedAt };
                rows[job.JobId] = row;
                db.Jobs.Add(row);
            }
            else if (row.Status == JobStatus.Read && row.RetrievedAt == job.RetrievedAt)
            {
                continue; // already stored by an earlier call
            }
            row.ApplyPosting(job);
        }

        foreach (var failure in details.Failures)
        {
            if (!rows.TryGetValue(failure.JobId, out var row))
            {
                row = new JobRow { JobId = failure.JobId, Url = failure.Url.AbsoluteUri, FirstSeenAt = details.UpdatedAt };
                rows[failure.JobId] = row;
                db.Jobs.Add(row);
            }
            if (row.Status == JobStatus.Read)
            {
                continue; // never replace a stored description with a later failure
            }
            if (row.Status != failure.Status || row.LastError != failure.Message)
            {
                row.Status = failure.Status == JobStatus.Blocked ? JobStatus.Blocked : JobStatus.Failed;
                row.LastError = failure.Message;
                row.LastAttemptAt = details.UpdatedAt;
            }
        }

        var listing = await db.Listings.SingleOrDefaultAsync(l => l.BatchId == details.BatchId, ct);
        if (listing is not null)
        {
            listing.DetailsUpdatedAt = details.UpdatedAt;
            listing.DetailsPending = details.Pending;
            listing.DetailsStoppedReason = details.StoppedReason;
        }

        await db.SaveChangesAsync(ct);
        return locations.Listing(details.BatchId);
    }
}
