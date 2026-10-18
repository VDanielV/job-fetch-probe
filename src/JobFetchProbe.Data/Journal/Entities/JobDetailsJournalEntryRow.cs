namespace JobFetchProbe.Data.Journal.Entities;

/// <summary>One thing that happened in a step 2 run: a job tried, a circuit breaker change, the start or the end.</summary>
public sealed class JobDetailsJournalEntryRow
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public DateTimeOffset At { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int Index { get; set; }
    public int Total { get; set; }
    public string? JobId { get; set; }
    public string? Title { get; set; }
    public string? Status { get; set; }
    public bool Gone { get; set; }
    public string? Message { get; set; }
    public int? WaitSeconds { get; set; }
}
