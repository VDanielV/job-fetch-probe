namespace JobFetchProbe.Models.Journal;

/// <summary>
/// A step 1 run (FetchJobListing) as the journal holds it: where it stands now, plus the entries asked for
/// (normally those after the last one a reader already has).
/// </summary>
/// <param name="Pages">Saved pages found in the input folder.</param>
/// <param name="PagesRead">Pages read with at least one job.</param>
/// <param name="PagesKept">Pages that could not be read and stay in the input folder.</param>
/// <param name="JobsStored">Unique job links stored as the run's listing.</param>
/// <param name="NewJobs">Of those, jobs not seen before.</param>
public sealed record JobListingJournalRun
{
    public long RunId { get; init; }
    public JournalStarter StartedBy { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public JournalRunState State { get; init; }
    public int Pages { get; init; }
    public int PagesRead { get; init; }
    public int PagesKept { get; init; }
    public int JobsStored { get; init; }
    public int NewJobs { get; init; }
    public string? ListingId { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<JobListingJournalEntry> Entries { get; init; } = [];
}
