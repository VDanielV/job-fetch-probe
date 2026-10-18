namespace JobFetchProbe.Models.Journal;

/// <summary>
/// A step 2 run (FetchJobDetails) as the journal holds it: where it stands now, plus the entries asked for
/// (normally those after the last one a reader already has).
/// </summary>
/// <param name="Total">Jobs without a description when the run started.</param>
/// <param name="ToRead">Jobs this run still has to try.</param>
/// <param name="CircuitOpenUntil">While the circuit is open: when the trial request is due.</param>
/// <param name="EtaSeconds">Estimated time left, from the average time per job so far.</param>
public sealed record JobDetailsJournalRun
{
    public long RunId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public JournalRunState State { get; init; }
    public int Total { get; init; }
    public int ToRead { get; init; }
    public int Fetched { get; init; }
    public int Failed { get; init; }
    public int Requests { get; init; }
    public JournalCircuitState CircuitState { get; init; }
    public DateTimeOffset? CircuitOpenUntil { get; init; }
    public int Openings { get; init; }
    public int StopAfterOpenings { get; init; }
    public int? EtaSeconds { get; init; }
    public string? Message { get; init; }

    /// <summary>When the run ended: jobs still without a description (tried again on the next run).</summary>
    public int? StillUnread { get; init; }

    /// <summary>When the run ended: jobs with a description in the database, in total.</summary>
    public int? TotalRead { get; init; }

    /// <summary>When the run failed: the type of the error (for example Microsoft.Data.Sqlite.SqliteException); the message holds its text.</summary>
    public string? ErrorType { get; init; }
    public IReadOnlyList<JobDetailsJournalEntry> Entries { get; init; } = [];
}
