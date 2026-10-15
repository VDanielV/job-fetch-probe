using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>Reads listings back from the database for the details step.</summary>
public sealed class SqliteListingStore(IDbContextFactory<JobsDbContext> contexts, SqliteLocations locations) : IListingStore
{
    public async Task<JobSearchBatch?> ReadAsync(string path, CancellationToken ct = default)
    {
        var batchId = SqliteLocations.BatchIdIn(path);
        if (batchId is null)
        {
            return null;
        }

        await using var db = await contexts.CreateDbContextAsync(ct);
        var listing = await db.Listings.AsNoTracking()
            .Include(l => l.Sources)
            .Include(l => l.Jobs)
            .AsSplitQuery()
            .SingleOrDefaultAsync(l => l.BatchId == batchId, ct);
        if (listing is null)
        {
            return null;
        }

        return new JobSearchBatch
        {
            BatchId = listing.BatchId,
            CreatedAt = listing.CreatedAt,
            Sources = listing.Sources.OrderBy(s => s.Position)
                .Select(s => new BatchSource(s.File, new Uri(s.SourceUrl), s.Query, s.TotalResults, s.Jobs, s.NewJobs))
                .ToList(),
            Jobs = listing.Jobs.OrderBy(j => j.Position)
                .Select(j => JobRowMapping.ToListing(j.JobId, j.Url, j.Title, j.Company, j.Location, j.ListedAt))
                .ToList(),
        };
    }

    public string? FindLatest()
    {
        using var db = contexts.CreateDbContext();
        // Batch IDs sort by time: yyyyMMdd_HHmmss.
        var batchId = db.Listings.OrderByDescending(l => l.BatchId).Select(l => l.BatchId).FirstOrDefault();
        return batchId is null ? null : locations.Listing(batchId);
    }

    public string? Resolve(string reference)
    {
        var batchId = SqliteLocations.BatchIdIn(reference);
        if (batchId is null)
        {
            return null;
        }
        using var db = contexts.CreateDbContext();
        return db.Listings.Any(l => l.BatchId == batchId) ? locations.Listing(batchId) : null;
    }
}
