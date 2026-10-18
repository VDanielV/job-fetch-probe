namespace JobFetchProbe.Models.Journal;

/// <summary>One thing that happened in a step 1 run. Ids grow in the order the entries were written.</summary>
/// <param name="Index">Position (1-based) of the page in this run.</param>
/// <param name="Total">Pages in this run.</param>
public sealed record JobListingJournalEntry(
    long Id,
    DateTimeOffset At,
    int Index,
    int Total,
    string File,
    JobListingJournalOutcome Outcome,
    int Jobs,
    int NewJobs,
    string? Message);
