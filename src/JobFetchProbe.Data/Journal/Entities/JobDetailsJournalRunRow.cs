namespace JobFetchProbe.Data.Journal.Entities;

/// <summary>One step 2 run in the journal: its current state. Enums are stored by name.</summary>
public sealed class JobDetailsJournalRunRow
{
    public long Id { get; set; }
    public string StartedBy { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public int Total { get; set; }
    public int ToRead { get; set; }
    public int Fetched { get; set; }
    public int Failed { get; set; }
    public int Requests { get; set; }
    public string CircuitState { get; set; } = string.Empty;
    public DateTimeOffset? CircuitOpenUntil { get; set; }
    public int Openings { get; set; }
    public int StopAfterOpenings { get; set; }
    public int? EtaSeconds { get; set; }
    public string? Message { get; set; }
    public int? StillUnread { get; set; }
    public int? TotalRead { get; set; }
    public string? ErrorType { get; set; }
    public List<JobDetailsJournalEntryRow> Entries { get; set; } = [];
}
