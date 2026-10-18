namespace JobFetchProbe.Models.Journal;

/// <summary>One line per run, of either step, for the list of recent runs.</summary>
/// <param name="Message">How the run ended, or what it is doing.</param>
public sealed record JournalRunSummary(
    JournalStep Step,
    long RunId,
    JournalStarter StartedBy,
    DateTimeOffset StartedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? FinishedAt,
    JournalRunState State,
    string? Message);
