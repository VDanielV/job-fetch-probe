using JobFetchProbe.Data.Journal.Entities;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Abstractions.Journal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Data.Journal.Repositories;

/// <summary>
/// The journal tables. Each call opens its own short-lived context, so the writer (the process that fetches) and any
/// reader (another process) never share one; SQLite in WAL mode lets them work at the same time. Reading a database
/// that the app has not upgraded yet (no journal tables) returns nothing instead of failing.
/// </summary>
public sealed class SqliteJournalRepository(IDbContextFactory<JobsDbContext> contexts, IOptions<DatabaseSettings> database) : IJournalRepository
{
    public bool IsAvailable => File.Exists(database.Value.FullPath);

    public async Task<long> StartJobListingRunAsync(JobListingJournalRun run, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = new JobListingJournalRunRow();
        run.CopyTo(row);
        db.Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task SaveJobListingRunAsync(JobListingJournalRun run, JobListingJournalEntry? entry, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Set<JobListingJournalRunRow>().SingleAsync(r => r.Id == run.RunId, ct);
        run.CopyTo(row);
        await db.SaveChangesAsync(ct);

        // Saved apart from the state, so a failed entry never keeps the run's final state from being stored.
        if (entry is not null)
        {
            db.Add(entry.ToRow(run.RunId));
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<long> StartJobDetailsRunAsync(JobDetailsJournalRun run, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = new JobDetailsJournalRunRow();
        run.CopyTo(row);
        db.Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task SaveJobDetailsRunAsync(JobDetailsJournalRun run, JobDetailsJournalEntry? entry, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Set<JobDetailsJournalRunRow>().SingleAsync(r => r.Id == run.RunId, ct);
        run.CopyTo(row);
        await db.SaveChangesAsync(ct);

        // Saved apart from the state, so a failed entry never keeps the run's final state from being stored.
        if (entry is not null)
        {
            db.Add(entry.ToRow(run.RunId));
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task TouchAsync(JournalStep step, long runId, DateTimeOffset at, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        if (step == JournalStep.JobListing)
        {
            await db.Set<JobListingJournalRunRow>().Where(r => r.Id == runId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.LastSeenAt, at), ct);
        }
        else
        {
            await db.Set<JobDetailsJournalRunRow>().Where(r => r.Id == runId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.LastSeenAt, at), ct);
        }
    }

    public async Task<IReadOnlyList<JournalRunSummary>> GetRunningAsync(JournalStep step, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var running = JournalRunState.Running.ToString();
        if (step == JournalStep.JobListing)
        {
            var rows = await db.Set<JobListingJournalRunRow>().AsNoTracking().Where(r => r.State == running).ToListAsync(ct);
            return rows.Select(r => Summary(JournalStep.JobListing, r.Id, r.StartedAt, r.LastSeenAt, r.FinishedAt, r.State, r.Message)).ToList();
        }
        else
        {
            var rows = await db.Set<JobDetailsJournalRunRow>().AsNoTracking().Where(r => r.State == running).ToListAsync(ct);
            return rows.Select(r => Summary(JournalStep.JobDetails, r.Id, r.StartedAt, r.LastSeenAt, r.FinishedAt, r.State, r.Message)).ToList();
        }
    }

    public async Task MarkAbandonedAsync(JournalStep step, long runId, DateTimeOffset at, string message, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var abandoned = JournalRunState.Abandoned.ToString();
        if (step == JournalStep.JobListing)
        {
            await db.Set<JobListingJournalRunRow>().Where(r => r.Id == runId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, abandoned).SetProperty(r => r.FinishedAt, at).SetProperty(r => r.Message, message), ct);
        }
        else
        {
            await db.Set<JobDetailsJournalRunRow>().Where(r => r.Id == runId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, abandoned).SetProperty(r => r.FinishedAt, at).SetProperty(r => r.Message, message).SetProperty(r => r.EtaSeconds, (int?)null), ct);
        }
    }

    public async Task PruneAsync(JournalStep step, int keep, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        if (step == JournalStep.JobListing)
        {
            var old = await db.Set<JobListingJournalRunRow>().OrderByDescending(r => r.Id).Skip(keep).Select(r => r.Id).ToListAsync(ct);
            if (old.Count > 0)
            {
                await db.Set<JobListingJournalEntryRow>().Where(e => old.Contains(e.RunId)).ExecuteDeleteAsync(ct);
                await db.Set<JobListingJournalRunRow>().Where(r => old.Contains(r.Id)).ExecuteDeleteAsync(ct);
            }
        }
        else
        {
            var old = await db.Set<JobDetailsJournalRunRow>().OrderByDescending(r => r.Id).Skip(keep).Select(r => r.Id).ToListAsync(ct);
            if (old.Count > 0)
            {
                await db.Set<JobDetailsJournalEntryRow>().Where(e => old.Contains(e.RunId)).ExecuteDeleteAsync(ct);
                await db.Set<JobDetailsJournalRunRow>().Where(r => old.Contains(r.Id)).ExecuteDeleteAsync(ct);
            }
        }
    }

    public async Task<IReadOnlyList<JournalRunSummary>> GetRecentRunsAsync(int take, CancellationToken ct = default)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync(ct);
            if (!await HasJournalAsync(db, ct))
            {
                return [];
            }
            var listing = await db.Set<JobListingJournalRunRow>().AsNoTracking().OrderByDescending(r => r.Id).Take(take).ToListAsync(ct);
            var details = await db.Set<JobDetailsJournalRunRow>().AsNoTracking().OrderByDescending(r => r.Id).Take(take).ToListAsync(ct);
            return listing
                .Select(r => Summary(JournalStep.JobListing, r.Id, r.StartedAt, r.LastSeenAt, r.FinishedAt, r.State, r.Message))
                .Concat(details.Select(r => Summary(JournalStep.JobDetails, r.Id, r.StartedAt, r.LastSeenAt, r.FinishedAt, r.State, r.Message)))
                .OrderByDescending(r => r.StartedAt)
                .Take(take)
                .ToList();
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            return [];
        }
    }

    public async Task<JobListingJournalRun?> GetJobListingRunAsync(long runId, long afterEntryId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync(ct);
            if (!await HasJournalAsync(db, ct))
            {
                return null;
            }
            var row = await db.Set<JobListingJournalRunRow>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId, ct);
            if (row is null)
            {
                return null;
            }
            var entries = await db.Set<JobListingJournalEntryRow>().AsNoTracking()
                .Where(e => e.RunId == runId && e.Id > afterEntryId).OrderBy(e => e.Id).ToListAsync(ct);
            return row.ToModel(entries);
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            return null;
        }
    }

    public async Task<JobDetailsJournalRun?> GetJobDetailsRunAsync(long runId, long afterEntryId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync(ct);
            if (!await HasJournalAsync(db, ct))
            {
                return null;
            }
            var row = await db.Set<JobDetailsJournalRunRow>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId, ct);
            if (row is null)
            {
                return null;
            }
            var entries = await db.Set<JobDetailsJournalEntryRow>().AsNoTracking()
                .Where(e => e.RunId == runId && e.Id > afterEntryId).OrderBy(e => e.Id).ToListAsync(ct);
            return row.ToModel(entries);
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            return null;
        }
    }

    private static JournalRunSummary Summary(JournalStep step, long id, DateTimeOffset startedAt, DateTimeOffset lastSeenAt, DateTimeOffset? finishedAt, string state, string? message) =>
        new(step, id, startedAt, lastSeenAt, finishedAt, JournalRowMapping.Parse<JournalRunState>(state), message);

    /// <summary>
    /// False while the database has no journal tables yet: the app adds them (migration AddJournal) on its next
    /// start. Checked first, so reading an older database does not log a failed command.
    /// </summary>
    private static async Task<bool> HasJournalAsync(JobsDbContext db, CancellationToken ct)
    {
        var tables = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name IN ('JobListingJournalRuns', 'JobDetailsJournalRuns')")
            .SingleAsync(ct);
        return tables == 2;
    }

    /// <summary>The database exists but the app has not added the journal tables yet (older schema).</summary>
    private static bool IsMissingTable(SqliteException ex) => ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);
}
