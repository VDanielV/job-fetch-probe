using JobFetchProbe.Data.Journal.Entities;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Data.Journal;

/// <summary>Copies between the journal's rows and the models. Enums are stored by name, so the tables read well.</summary>
internal static class JournalRowMapping
{
    public static void CopyTo(this JobListingJournalRun run, JobListingJournalRunRow row)
    {
        row.StartedBy = run.StartedBy.ToString();
        row.StartedAt = run.StartedAt;
        row.LastSeenAt = run.LastSeenAt;
        row.FinishedAt = run.FinishedAt;
        row.State = run.State.ToString();
        row.Pages = run.Pages;
        row.PagesRead = run.PagesRead;
        row.PagesKept = run.PagesKept;
        row.JobsStored = run.JobsStored;
        row.NewJobs = run.NewJobs;
        row.ListingId = run.ListingId;
        row.Message = run.Message;
        row.ErrorType = run.ErrorType;
    }

    public static void CopyTo(this JobDetailsJournalRun run, JobDetailsJournalRunRow row)
    {
        row.StartedBy = run.StartedBy.ToString();
        row.StartedAt = run.StartedAt;
        row.LastSeenAt = run.LastSeenAt;
        row.FinishedAt = run.FinishedAt;
        row.State = run.State.ToString();
        row.Total = run.Total;
        row.ToRead = run.ToRead;
        row.Fetched = run.Fetched;
        row.Failed = run.Failed;
        row.Requests = run.Requests;
        row.CircuitState = run.CircuitState.ToString();
        row.CircuitOpenUntil = run.CircuitOpenUntil;
        row.Openings = run.Openings;
        row.StopAfterOpenings = run.StopAfterOpenings;
        row.EtaSeconds = run.EtaSeconds;
        row.Message = run.Message;
        row.StillUnread = run.StillUnread;
        row.TotalRead = run.TotalRead;
        row.ErrorType = run.ErrorType;
    }

    public static JobListingJournalEntryRow ToRow(this JobListingJournalEntry entry, long runId) => new()
    {
        RunId = runId,
        At = entry.At,
        Index = entry.Index,
        Total = entry.Total,
        File = entry.File,
        Outcome = entry.Outcome.ToString(),
        Jobs = entry.Jobs,
        NewJobs = entry.NewJobs,
        Message = entry.Message,
    };

    public static JobDetailsJournalEntryRow ToRow(this JobDetailsJournalEntry entry, long runId) => new()
    {
        RunId = runId,
        At = entry.At,
        Kind = entry.Kind.ToString(),
        Index = entry.Index,
        Total = entry.Total,
        JobId = entry.JobId,
        Title = entry.Title,
        Status = entry.Status?.ToString(),
        Gone = entry.Gone,
        Message = entry.Message,
        WaitSeconds = entry.WaitSeconds,
    };

    public static JobListingJournalRun ToModel(this JobListingJournalRunRow row, IReadOnlyList<JobListingJournalEntryRow> entries) => new()
    {
        RunId = row.Id,
        StartedBy = Parse<JournalStarter>(row.StartedBy),
        StartedAt = row.StartedAt,
        LastSeenAt = row.LastSeenAt,
        FinishedAt = row.FinishedAt,
        State = Parse<JournalRunState>(row.State),
        Pages = row.Pages,
        PagesRead = row.PagesRead,
        PagesKept = row.PagesKept,
        JobsStored = row.JobsStored,
        NewJobs = row.NewJobs,
        ListingId = row.ListingId,
        Message = row.Message,
        ErrorType = row.ErrorType,
        Entries = entries
            .Select(e => new JobListingJournalEntry(e.Id, e.At, e.Index, e.Total, e.File, Parse<JobListingJournalOutcome>(e.Outcome), e.Jobs, e.NewJobs, e.Message))
            .ToList(),
    };

    public static JobDetailsJournalRun ToModel(this JobDetailsJournalRunRow row, IReadOnlyList<JobDetailsJournalEntryRow> entries) => new()
    {
        RunId = row.Id,
        StartedBy = Parse<JournalStarter>(row.StartedBy),
        StartedAt = row.StartedAt,
        LastSeenAt = row.LastSeenAt,
        FinishedAt = row.FinishedAt,
        State = Parse<JournalRunState>(row.State),
        Total = row.Total,
        ToRead = row.ToRead,
        Fetched = row.Fetched,
        Failed = row.Failed,
        Requests = row.Requests,
        CircuitState = Parse<JournalCircuitState>(row.CircuitState),
        CircuitOpenUntil = row.CircuitOpenUntil,
        Openings = row.Openings,
        StopAfterOpenings = row.StopAfterOpenings,
        EtaSeconds = row.EtaSeconds,
        Message = row.Message,
        StillUnread = row.StillUnread,
        TotalRead = row.TotalRead,
        ErrorType = row.ErrorType,
        Entries = entries
            .Select(e => new JobDetailsJournalEntry(
                e.Id, e.At, Parse<JobDetailsJournalEntryKind>(e.Kind), e.Index, e.Total, e.JobId, e.Title,
                e.Status is null ? null : Parse<JobResultStatus>(e.Status), e.Gone, e.Message, e.WaitSeconds))
            .ToList(),
    };

    public static T Parse<T>(string value) where T : struct, Enum => Enum.Parse<T>(value);
}
