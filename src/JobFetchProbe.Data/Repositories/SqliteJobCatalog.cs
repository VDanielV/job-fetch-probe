using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Data.Repositories;

/// <summary>Reads the Jobs table for the web app and the API. Never writes: each call opens a short-lived, no-tracking context.</summary>
public sealed class SqliteJobCatalog(IDbContextFactory<JobsDbContext> contexts, IOptions<DatabaseSettings> database) : IJobCatalog
{
    /// <summary>The connection is read-only and never creates the file, so the database exists only once the app ran.</summary>
    public bool IsAvailable => File.Exists(database.Value.FullPath);

    public async Task<JobSummaryPage> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var total = await db.Jobs.CountAsync(ct);
        var pages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pages);

        // LinkedIn job IDs grow over time, so the highest IDs are the newest postings. (SQLite cannot sort by the
        // DateTimeOffset columns, and sorting by ID keeps paging in the database.)
        var jobs = await db.Jobs.AsNoTracking()
            .OrderByDescending(j => j.JobId.Length)
            .ThenByDescending(j => j.JobId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobSummary(j.JobId, j.Title, j.Company, j.Location, j.Posted, j.Status))
            .ToListAsync(ct);

        return new JobSummaryPage(jobs, page, pageSize, total);
    }

    public async Task<StoredJob?> GetAsync(string jobId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Jobs.AsNoTracking().Include(j => j.Details).SingleOrDefaultAsync(j => j.JobId == jobId, ct);
        if (row is null)
        {
            return null;
        }

        return new StoredJob
        {
            JobId = row.JobId,
            Url = new Uri(row.Url),
            Title = row.Title,
            Company = row.Company,
            Location = row.Location,
            Posted = row.Posted,
            EmploymentType = row.EmploymentType,
            Details = row.Details.OrderBy(d => d.Position).Select(d => new JobDetail(d.Label, d.Value)).ToList(),
            Description = row.Description,
            Status = row.Status,
            LastError = row.LastError,
            FirstSeenAt = row.FirstSeenAt,
            RetrievedAt = row.RetrievedAt,
            LastAttemptAt = row.LastAttemptAt,
        };
    }
}
