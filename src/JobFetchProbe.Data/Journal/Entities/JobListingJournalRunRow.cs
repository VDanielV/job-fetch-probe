namespace JobFetchProbe.Data.Journal.Entities;

/// <summary>One step 1 run in the journal: its current state. Enums are stored by name.</summary>
public sealed class JobListingJournalRunRow
{
    public long Id { get; set; }
    public string StartedBy { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public int Pages { get; set; }
    public int PagesRead { get; set; }
    public int PagesKept { get; set; }
    public int JobsStored { get; set; }
    public int NewJobs { get; set; }
    public string? ListingId { get; set; }
    public string? Message { get; set; }
    public string? ErrorType { get; set; }
    public List<JobListingJournalEntryRow> Entries { get; set; } = [];
}
