using JobFetchProbe.Data.Entities;
using JobFetchProbe.Data.Fit;
using JobFetchProbe.Data.Journal;
using JobFetchProbe.Data.Journal.Entities;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Abstractions.RunHistory;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data.RunHistory;

/// <summary>
/// Reads the history of past acquisitions: the journal's runs, the listings, and each listed job as it is now. Changes
/// nothing. A job is new in a listing when its FirstSeenAt is the listing's CreatedAt: step 1 gives a new job exactly
/// that time, so this holds for listings stored before the journal existed too. Dates are compared here, not in SQL
/// (SQLite cannot compare DateTimeOffset).
/// </summary>
public sealed class SqliteRunHistoryRepository(IDbContextFactory<JobsDbContext> contexts) : IRunHistoryRepository
{
    private const string Assessed = nameof(JobDetailsJournalEntryKind.Assessed);

    public async Task<RunHistorySource> GetSourceAsync(CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var listingRows = await db.Set<JobListingJournalRunRow>().AsNoTracking().ToListAsync(ct);
        var listingRuns = listingRows.Select(r => r.ToModel([])).ToList();

        var detailsRows = await db.Set<JobDetailsJournalRunRow>().AsNoTracking().ToListAsync(ct);
        var scored = await db.Set<JobDetailsJournalEntryRow>().AsNoTracking()
            .Where(e => e.Kind == Assessed && e.Message != null && !e.Message.StartsWith("Not scored"))
            .GroupBy(e => e.RunId)
            .Select(g => new { RunId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RunId, g => g.Count, ct);
        var detailsRuns = new List<DetailsRunRecord>();
        foreach (var row in detailsRows)
        {
            detailsRuns.Add(new DetailsRunRecord(row.ToModel([]), scored.GetValueOrDefault(row.Id)));
        }

        var listings = await db.Listings.AsNoTracking().Select(l => new { l.BatchId, l.CreatedAt }).ToListAsync(ct);
        var pages = await db.Set<ListingSourceRow>().AsNoTracking()
            .GroupBy(s => s.BatchId)
            .Select(g => new { BatchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BatchId, g => g.Count, ct);
        var links = await db.ListingJobs.AsNoTracking()
            .Join(db.Jobs, lj => lj.JobId, j => j.Id, (lj, j) => new { lj.BatchId, j.FirstSeenAt })
            .ToListAsync(ct);
        var linksByListing = links.ToLookup(l => l.BatchId);

        var tallies = new List<ListingTally>();
        foreach (var listing in listings)
        {
            var own = linksByListing[listing.BatchId].ToList();
            var isNew = own.Count(l => l.FirstSeenAt == listing.CreatedAt);
            tallies.Add(new ListingTally(listing.BatchId, listing.CreatedAt, pages.GetValueOrDefault(listing.BatchId), own.Count, isNew));
        }

        return new RunHistorySource(listingRuns, detailsRuns, tallies);
    }

    public async Task<ListingLinksRecord?> GetListingLinksAsync(string listingId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var listing = await db.Listings.AsNoTracking().SingleOrDefaultAsync(l => l.BatchId == listingId, ct);
        if (listing is null)
        {
            return null;
        }

        var pages = await db.Set<ListingSourceRow>().AsNoTracking()
            .Where(s => s.BatchId == listingId)
            .OrderBy(s => s.Position)
            .Select(s => new ListingPageRecord(s.File, s.NewJobs))
            .ToListAsync(ct);

        var rows = await db.ListingJobs.AsNoTracking()
            .Where(lj => lj.BatchId == listingId)
            .Join(db.Jobs, lj => lj.JobId, j => j.Id, (lj, j) => new
            {
                lj.JobId,
                lj.Position,
                j.Source,
                j.SourceJobId,
                Title = j.Title ?? lj.Title,
                Company = j.Company ?? lj.Company,
                j.FirstSeenAt,
                j.Status,
                j.RetrievedAt,
                j.LastAttemptAt,
            })
            .OrderBy(r => r.Position)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.JobId).ToList();
        var archived = await db.ArchivedJobs.AsNoTracking()
            .Where(a => ids.Contains(a.JobId))
            .ToDictionaryAsync(a => a.JobId, a => a.ArchivedAt, ct);
        var applied = await db.Applications.AsNoTracking()
            .Where(a => ids.Contains(a.JobId))
            .ToDictionaryAsync(a => a.JobId, a => a.AppliedAt, ct);
        var fits = await LatestFits.ReadAsync(db, ids, ct);

        var links = new List<ListingLinkRecord>();
        foreach (var r in rows)
        {
            DateTimeOffset? archivedAt = null;
            if (archived.TryGetValue(r.JobId, out var a))
            {
                archivedAt = a;
            }
            DateTimeOffset? appliedAt = null;
            if (applied.TryGetValue(r.JobId, out var p))
            {
                appliedAt = p;
            }
            links.Add(new ListingLinkRecord(
                r.JobId, r.Position, r.Source, r.SourceJobId, r.Title, r.Company, r.FirstSeenAt, r.Status,
                r.RetrievedAt, r.LastAttemptAt, archivedAt, appliedAt, fits.GetValueOrDefault(r.JobId)));
        }

        return new ListingLinksRecord(listing.BatchId, listing.CreatedAt, pages, links);
    }
}
