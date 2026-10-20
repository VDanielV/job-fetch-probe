using JobFetchProbe.Models.Applications;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Abstractions.Applications;
using JobFetchProbe.Services.Catalog;

namespace JobFetchProbe.Services.Applications;

/// <summary>The rules for applications; the data layer keeps them (<see cref="IApplicationRepository"/>).</summary>
public sealed class ApplicationTracker(IApplicationRepository applications, IJobCatalog catalog, TimeProvider time) : IApplicationTracker
{
    public const int MaxNoteLength = 2000;
    public const string AppliedLine = "Marked as applied";

    public async Task<Application?> ApplyAsync(ApplyRequest request, CancellationToken ct = default)
    {
        if (request.JobId <= 0)
        {
            throw new ApplicationRequestException("jobId", "Choose the job you applied to.");
        }
        var jobId = request.JobId;
        EnsureDatabase();

        var outcome = await applications.AddAsync(jobId, time.GetLocalNow(), AppliedLine, ct);
        if (outcome == ApplyOutcome.NotFound)
        {
            return null;
        }
        if (outcome == ApplyOutcome.AlreadyApplied)
        {
            throw new ApplicationConflictException($"Job {jobId} is already in your applications.");
        }
        if (outcome == ApplyOutcome.Archived)
        {
            throw new ApplicationConflictException($"Job {jobId} is archived, so it cannot be marked as applied.");
        }
        return await GetAsync(jobId, ct);
    }

    public async Task<bool> UndoAsync(long jobId, bool withNotes, CancellationToken ct = default)
    {
        EnsureDatabase();
        var record = await applications.GetAsync(jobId, ct);
        if (record is null)
        {
            return false;
        }
        if (record.NoteCount > 0 && !withNotes)
        {
            var message = $"This application has {record.NoteCount} notes. Undo deletes them too.";
            if (record.NoteCount == 1)
            {
                message = "This application has 1 note. Undo deletes it too.";
            }
            throw new ApplicationConflictException(message, record.NoteCount);
        }
        return await applications.DeleteAsync(record.JobId, ct);
    }

    public async Task<ApplicationPage> GetPageAsync(
        bool closed, ApplicationStage? stage, bool flagged, int page, int pageSize, ApplicationSort sort = ApplicationSort.FollowUp,
        CancellationToken ct = default)
    {
        EnsureDatabase();
        var today = time.GetLocalNow();
        var all = (await applications.ListAsync(ct)).Select(r => ToSummary(r, today)).ToList();

        var open = all.Where(a => !ApplicationStages.IsClosed(a.Stage)).ToList();
        var shut = all.Where(a => ApplicationStages.IsClosed(a.Stage)).ToList();
        var stages = Enum.GetValues<ApplicationStage>()
            .Select(s => new ApplicationStageCount(s, all.Count(a => a.Stage == s)))
            .ToList();

        IEnumerable<ApplicationSummary> listed;
        if (closed)
        {
            // Most recently closed first.
            listed = shut.OrderByDescending(a => a.StageChangedAt);
        }
        else
        {
            // Flagged first (No answer, then Follow up), then the newest applications.
            listed = open
                .OrderBy(a => FlagOrder(a.Flag))
                .ThenByDescending(a => a.AppliedAt);
        }
        if (stage is not null)
        {
            listed = listed.Where(a => a.Stage == stage.Value);
        }
        if (flagged)
        {
            listed = listed.Where(a => a.Flag != FollowUpFlag.None);
        }

        if (sort == ApplicationSort.Fit)
        {
            // Best fit first; never assessed last. Stable: ties keep the order above.
            listed = listed.OrderBy(a => a.Fit is null ? 1 : 0).ThenByDescending(a => a.Fit?.Fit ?? 0);
        }

        var list = listed.ToList();
        var pages = Math.Max(1, (list.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pages);
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var flaggedCount = open.Count(a => a.Flag != FollowUpFlag.None);
        return new ApplicationPage(items, page, pageSize, list.Count, open.Count, shut.Count, flaggedCount, stages);
    }

    public async Task<Application?> GetAsync(long jobId, CancellationToken ct = default)
    {
        EnsureDatabase();
        var record = await applications.GetAsync(jobId, ct);
        if (record is null)
        {
            return null;
        }
        var notes = await applications.GetNotesAsync(record.JobId, ct);
        return new Application(ToSummary(record, time.GetLocalNow()), notes);
    }

    public async Task<Application?> SetStageAsync(long jobId, StageRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Stage))
        {
            throw new ApplicationRequestException("stage", "Unknown stage.");
        }
        EnsureDatabase();
        var record = await applications.GetAsync(jobId, ct);
        if (record is null)
        {
            return null;
        }
        if (record.Stage != request.Stage)
        {
            var line = ApplicationStages.Label(record.Stage) + " → " + ApplicationStages.Label(request.Stage);
            await applications.ChangeStageAsync(record.JobId, request.Stage, time.GetLocalNow(), line, ct);
        }
        return await GetAsync(record.JobId, ct);
    }

    public async Task<Application?> AddNoteAsync(long jobId, NoteRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ApplicationRequestException("text", "Write the note first.");
        }
        var text = request.Text.Trim();
        if (text.Length > MaxNoteLength)
        {
            throw new ApplicationRequestException("text", $"A note can have at most {MaxNoteLength} characters.");
        }
        EnsureDatabase();
        if (!await applications.AddNoteAsync(jobId, text, time.GetLocalNow(), ct))
        {
            return null;
        }
        return await GetAsync(jobId, ct);
    }

    public async Task<ApplicationsSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        EnsureDatabase();
        var today = time.GetLocalNow();
        var open = (await applications.ListAsync(ct))
            .Where(r => !ApplicationStages.IsClosed(r.Stage))
            .Select(r => ToSummary(r, today))
            .ToList();
        return new ApplicationsSummary(open.Count, open.Count(a => a.Flag != FollowUpFlag.None));
    }

    private void EnsureDatabase()
    {
        if (!catalog.IsAvailable)
        {
            throw new JobStoreUnavailableException();
        }
    }

    private static ApplicationSummary ToSummary(ApplicationRecord r, DateTimeOffset today)
    {
        var days = FollowUpRules.DaysSince(r.AppliedAt, today);
        return new ApplicationSummary(
            r.JobId, r.Title, r.Company, r.Location, r.Stage, r.AppliedAt, r.StageChangedAt, days,
            FollowUpRules.Flag(r.Stage, days), FollowUpRules.FlagFrom(r.Stage, r.AppliedAt, today), r.LastNote, r.NoteCount, r.Fit);
    }

    private static int FlagOrder(FollowUpFlag flag)
    {
        if (flag == FollowUpFlag.NoAnswer)
        {
            return 0;
        }
        if (flag == FollowUpFlag.FollowUp)
        {
            return 1;
        }
        return 2;
    }
}
