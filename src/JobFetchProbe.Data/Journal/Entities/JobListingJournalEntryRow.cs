namespace JobFetchProbe.Data.Journal.Entities;

/// <summary>One thing that happened in a step 1 run.</summary>
public sealed class JobListingJournalEntryRow
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public DateTimeOffset At { get; set; }
    public int Index { get; set; }
    public int Total { get; set; }
    public string File { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int Jobs { get; set; }
    public int NewJobs { get; set; }
    public string? Message { get; set; }
}
