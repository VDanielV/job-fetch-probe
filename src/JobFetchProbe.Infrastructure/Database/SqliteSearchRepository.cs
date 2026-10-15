using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Infrastructure.Database;

/// <summary>The search step's storage: one row per search, its cards replaced on every read.</summary>
public sealed class SqliteSearchRepository(IDbContextFactory<JobsDbContext> contexts, SqliteLocations locations) : ISearchRepository
{
    public async Task<string> SaveAsync(JobSearch search, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Overwrite, as the JSON file is overwritten: drop the old cards first.
        await db.Searches.Where(s => s.SearchId == search.SearchId).ExecuteDeleteAsync(ct);

        await db.UpsertCardsAsync(search.Jobs, search.RetrievedAt, ct);
        db.Searches.Add(new SearchRow
        {
            SearchId = search.SearchId,
            SourceUrl = search.SourceUrl.AbsoluteUri,
            Query = search.Query,
            TotalResults = search.TotalResults,
            ParsedFrom = search.ParsedFrom,
            RetrievedAt = search.RetrievedAt,
            Jobs = search.Jobs.Select((c, i) => new SearchJobRow
            {
                SearchId = search.SearchId,
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
        return locations.Search(search.SearchId);
    }
}
