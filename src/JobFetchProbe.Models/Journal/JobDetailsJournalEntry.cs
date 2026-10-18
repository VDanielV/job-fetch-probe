using JobFetchProbe.Models.Jobs;

namespace JobFetchProbe.Models.Journal;

/// <summary>One thing that happened in a step 2 run. Ids grow in the order the entries were written.</summary>
/// <param name="Index">For a job: its position (1-based) in this run's list.</param>
/// <param name="Status">For a job: Read, Blocked or Failed.</param>
/// <param name="Gone">For a job: the page no longer exists (404/410); it is not tried again.</param>
/// <param name="WaitSeconds">For CircuitOpened: the cooldown.</param>
public sealed record JobDetailsJournalEntry(
    long Id,
    DateTimeOffset At,
    JobDetailsJournalEntryKind Kind,
    int Index,
    int Total,
    string? JobId,
    string? Title,
    JobResultStatus? Status,
    bool Gone,
    string? Message,
    int? WaitSeconds);
