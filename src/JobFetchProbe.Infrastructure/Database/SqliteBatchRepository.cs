using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>
/// The inbox step's storage: one row per listing in Listings, its pages in ListingSources, its cards in ListingJobs,
/// and a row in Jobs for every job not seen before. Never overwrites: two runs in the same second get "_2", "_3"…
/// </summary>
public sealed class SqliteBatchRepository(IDbContextFactory<JobsDbContext> contexts, SqliteLocations locations) : IBatchRepository
{
    public async Task<string> SaveAsync(JobSearchBatch batch, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var batchId = batch.BatchId;
        for (var n = 2; await db.Listings.AnyAsync(l => l.BatchId == batchId, ct); n++)
        {
            batchId = $"{batch.BatchId}_{n}";
        }

        await db.UpsertCardsAsync(batch.Jobs, batch.CreatedAt, ct);
        db.Listings.Add(new ListingRow
        {
            BatchId = batchId,
            CreatedAt = batch.CreatedAt,
            Sources = batch.Sources.Select((s, i) => new ListingSourceRow
            {
                BatchId = batchId,
                Position = i,
                File = s.File,
                SourceUrl = s.SourceUrl.AbsoluteUri,
                Query = s.Query,
                TotalResults = s.TotalResults,
                Jobs = s.Jobs,
                NewJobs = s.NewJobs,
            }).ToList(),
            Jobs = batch.Jobs.Select((c, i) => new ListingJobRow
            {
                BatchId = batchId,
                JobId = c.JobId,
                Position = i,
                Url = c.Url.AbsoluteUri,
                Title = c.Title,
                Company = c.Company,
                Location = c.Location,
                ListedAt = c.ListedAt,
            }).ToList(),
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return locations.Listing(batchId);
    }
}
