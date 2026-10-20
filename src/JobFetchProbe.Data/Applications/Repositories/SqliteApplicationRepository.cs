using JobFetchProbe.Data.Applications.Entities;
using JobFetchProbe.Data.Fit;
using JobFetchProbe.Models.Applications;
using JobFetchProbe.Models.Fit;
using JobFetchProbe.Services.Abstractions.Applications;
using Microsoft.EntityFrameworkCore;

namespace JobFetchProbe.Data.Applications.Repositories;

/// <summary>
/// Keeps the applications in the Applications and ApplicationNotes tables. Never changes Jobs. Dates are sorted here,
/// not in SQL: SQLite cannot order by DateTimeOffset.
/// </summary>
public sealed class SqliteApplicationRepository(IDbContextFactory<JobsDbContext> contexts) : IApplicationRepository
{
    private static readonly string NoteKind = ApplicationNoteKind.Note.ToString();

    public async Task<ApplyOutcome> AddAsync(long jobId, DateTimeOffset at, string firstLine, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, ct))
        {
            return ApplyOutcome.NotFound;
        }
        if (await db.Applications.AnyAsync(a => a.JobId == jobId, ct))
        {
            return ApplyOutcome.AlreadyApplied;
        }
        if (await db.ArchivedJobs.AnyAsync(a => a.JobId == jobId, ct))
        {
            return ApplyOutcome.Archived;
        }

        var row = new ApplicationRow { JobId = jobId, AppliedAt = at, Stage = ApplicationStage.Applied.ToString(), StageChangedAt = at };
        row.Notes.Add(new ApplicationNoteRow { JobId = jobId, At = at, Kind = ApplicationNoteKind.Applied.ToString(), Text = firstLine });
        db.Applications.Add(row);

        // One SaveChanges: the application and its first line together, or neither.
        await db.SaveChangesAsync(ct);
        return ApplyOutcome.Added;
    }

    public async Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var rows = await db.Applications.AsNoTracking()
            .Join(db.Jobs.AsNoTracking(), a => a.JobId, j => j.Id, (a, j) => new { Application = a, j.Title, j.Company, j.Location })
            .ToListAsync(ct);
        var notes = await db.ApplicationNotes.AsNoTracking()
            .Where(n => n.Kind == NoteKind)
            .Select(n => new { n.JobId, n.Id, n.At, n.Text })
            .ToListAsync(ct);
        var byJob = notes.ToLookup(n => n.JobId);
        var fits = await LatestFits.ReadAsync(db, rows.Select(r => r.Application.JobId).ToList(), ct);

        return rows
            .Select(r =>
            {
                var own = byJob[r.Application.JobId].OrderByDescending(n => n.At).ThenByDescending(n => n.Id).ToList();
                return ToRecord(
                    r.Application, r.Title, r.Company, r.Location, own.FirstOrDefault()?.Text, own.Count, fits.GetValueOrDefault(r.Application.JobId));
            })
            .ToList();
    }

    public async Task<ApplicationRecord?> GetAsync(long jobId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Applications.AsNoTracking()
            .Join(db.Jobs.AsNoTracking(), a => a.JobId, j => j.Id, (a, j) => new { Application = a, j.Title, j.Company, j.Location })
            .SingleOrDefaultAsync(r => r.Application.JobId == jobId, ct);
        if (row is null)
        {
            return null;
        }
        var own = (await db.ApplicationNotes.AsNoTracking()
                .Where(n => n.JobId == jobId && n.Kind == NoteKind)
                .Select(n => new { n.Id, n.At, n.Text })
                .ToListAsync(ct))
            .OrderByDescending(n => n.At)
            .ThenByDescending(n => n.Id)
            .ToList();
        var fits = await LatestFits.ReadAsync(db, [jobId], ct);
        return ToRecord(row.Application, row.Title, row.Company, row.Location, own.FirstOrDefault()?.Text, own.Count, fits.GetValueOrDefault(jobId));
    }

    public async Task<IReadOnlyList<ApplicationNote>> GetNotesAsync(long jobId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var rows = await db.ApplicationNotes.AsNoTracking().Where(n => n.JobId == jobId).ToListAsync(ct);
        return rows
            .OrderByDescending(n => n.At)
            .ThenByDescending(n => n.Id)
            .Select(n => n.ToModel())
            .ToList();
    }

    public async Task<bool> ChangeStageAsync(long jobId, ApplicationStage stage, DateTimeOffset at, string line, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Applications.SingleOrDefaultAsync(a => a.JobId == jobId, ct);
        if (row is null)
        {
            return false;
        }
        row.Stage = stage.ToString();
        row.StageChangedAt = at;
        db.ApplicationNotes.Add(new ApplicationNoteRow { JobId = jobId, At = at, Kind = ApplicationNoteKind.Stage.ToString(), Text = line });
        await db.SaveChangesAsync(ct); // the stage and its line together
        return true;
    }

    public async Task<bool> AddNoteAsync(long jobId, string text, DateTimeOffset at, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        if (!await db.Applications.AnyAsync(a => a.JobId == jobId, ct))
        {
            return false;
        }
        db.ApplicationNotes.Add(new ApplicationNoteRow { JobId = jobId, At = at, Kind = NoteKind, Text = text });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(long jobId, CancellationToken ct = default)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.Applications.Include(a => a.Notes).SingleOrDefaultAsync(a => a.JobId == jobId, ct);
        if (row is null)
        {
            return false;
        }
        db.Applications.Remove(row); // its notes go with it (cascade)
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static ApplicationRecord ToRecord(
        ApplicationRow row, string? title, string? company, string? location, string? lastNote, int noteCount, FitScore? fit)
    {
        return new ApplicationRecord(
            row.JobId, title, company, location, Enum.Parse<ApplicationStage>(row.Stage), row.AppliedAt, row.StageChangedAt,
            lastNote, noteCount, fit);
    }
}
