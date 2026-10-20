using System.Text.RegularExpressions;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Models.RunHistory;
using JobFetchProbe.Services.Abstractions.RunHistory;

namespace JobFetchProbe.Services.RunHistory;

/// <summary>
/// Builds the list of past acquisitions: each step 1 run with the step 2 run that followed it (the first one started
/// after it and before the next step 1 run, as on the Acquire page), plus the listings stored by step 1 that no
/// journal run refers to (stored before the journal existed, or pruned). Imports such as <c>notion-migration</c> are
/// not runs and are left out.
/// </summary>
internal static partial class RunPairing
{
    public static List<RunHistoryLine> Build(RunHistorySource source)
    {
        var tallies = source.Listings.ToDictionary(l => l.ListingId, StringComparer.Ordinal);
        var listingRuns = source.ListingRuns.OrderBy(r => r.StartedAt).ToList();
        var detailsRuns = source.DetailsRuns.OrderBy(d => d.Run.StartedAt).ToList();
        var lines = new List<RunHistoryLine>();

        for (var i = 0; i < listingRuns.Count; i++)
        {
            var listing = listingRuns[i];
            DateTimeOffset? next = null;
            if (i + 1 < listingRuns.Count)
            {
                next = listingRuns[i + 1].StartedAt;
            }
            var details = detailsRuns.FirstOrDefault(d => d.Run.StartedAt >= listing.StartedAt && (next is null || d.Run.StartedAt < next));
            ListingTally? tally = null;
            if (listing.ListingId is not null)
            {
                tally = tallies.GetValueOrDefault(listing.ListingId);
            }
            lines.Add(LineOf(listing, details, tally));
        }

        var referenced = listingRuns.Where(r => r.ListingId is not null).Select(r => r.ListingId!).ToHashSet(StringComparer.Ordinal);
        foreach (var tally in source.Listings)
        {
            if (!referenced.Contains(tally.ListingId) && RunListingId().IsMatch(tally.ListingId))
            {
                lines.Add(new RunHistoryLine(null, null, tally.ListingId, tally.CreatedAt, tally.CreatedAt, null,
                    tally.Pages, tally.Pages, tally.Links, tally.New, tally.Links - tally.New, 0, 0, 0, 0,
                    "Stored before the journal existed: only the job links are known.", false));
            }
        }

        return lines.OrderByDescending(l => l.StartedAt).ToList();
    }

    private static RunHistoryLine LineOf(JobListingJournalRun listing, DetailsRunRecord? details, ListingTally? tally)
    {
        var links = 0;
        var isNew = 0;
        if (tally is not null)
        {
            links = tally.Links;
            isNew = tally.New;
        }

        if (details is null)
        {
            var empty = listing.Pages == 0;
            return new RunHistoryLine(listing.RunId, null, listing.ListingId, listing.StartedAt, listing.FinishedAt, listing.State,
                listing.Pages, listing.PagesRead, links, isNew, links - isNew, 0, 0, 0, 0, listing.Message, empty);
        }

        var run = details.Run;
        var nothingTried = run.Fetched == 0 && run.Failed == 0 && run.Requests == 0;
        var finished = run.FinishedAt;
        if (finished is null)
        {
            finished = listing.FinishedAt;
        }
        return new RunHistoryLine(listing.RunId, run.RunId, listing.ListingId, listing.StartedAt, finished, run.State,
            listing.Pages, listing.PagesRead, links, isNew, links - isNew, run.Fetched, run.Failed, details.FitScored, run.Openings,
            run.Message, listing.Pages == 0 && nothingTried);
    }

    /// <summary>A listing stored by step 1: yyyyMMdd_HHmmss, with _2, _3… when two runs share a second.</summary>
    [GeneratedRegex(@"^\d{8}_\d{6}(_\d+)?$")]
    private static partial Regex RunListingId();
}
