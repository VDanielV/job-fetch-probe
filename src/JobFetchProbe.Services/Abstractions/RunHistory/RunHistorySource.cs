using JobFetchProbe.Models.Journal;

namespace JobFetchProbe.Services.Abstractions.RunHistory;

/// <summary>The journal's runs of both steps (without entries) and the stored listings, as the history needs them.</summary>
public sealed record RunHistorySource(
    IReadOnlyList<JobListingJournalRun> ListingRuns,
    IReadOnlyList<DetailsRunRecord> DetailsRuns,
    IReadOnlyList<ListingTally> Listings);
