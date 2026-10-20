using System.Globalization;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Models.RunHistory;
using JobFetchProbe.Services.Abstractions.RunHistory;

namespace JobFetchProbe.Services.RunHistory;

/// <summary>
/// What step 2 did, in one run, with one job of the run's listing. From the run's journal entries when the job was
/// tried (its last try counts; the journal names jobs by the site's ID); otherwise from the job's dates, compared with
/// the start of the run: applied, archived or read before it means it was not asked for on purpose.
/// </summary>
internal static class Step2Outcome
{
    private const string External = "External";
    private const string Gone = "Gone";

    /// <param name="details">The step 2 run with all its entries; null when it did not run or has no journal.</param>
    /// <param name="hasJournal">False for a listing stored before the journal existed.</param>
    public static (RunJobStep2Kind Kind, string Text) Of(ListingLinkRecord link, JobDetailsJournalRun? details, bool hasJournal)
    {
        if (!hasJournal)
        {
            return (RunJobStep2Kind.Unknown, "Not known: this run has no journal");
        }
        if (details is null)
        {
            return (RunJobStep2Kind.NotReached, "Not reached: step 2 did not run");
        }

        var tries = details.Entries.Where(e => e.Kind == JobDetailsJournalEntryKind.Job && e.JobId == link.SourceJobId).ToList();
        if (tries.Count > 0)
        {
            return Tried(tries[^1], details.Entries, link.SourceJobId, tries.Count);
        }

        var at = details.StartedAt;
        if (link.AppliedAt is { } applied && applied < at)
        {
            return (RunJobStep2Kind.NotAsked, $"Not asked for: applied on {Day(applied, at)}");
        }
        if (link.ArchivedAt is { } archived && archived < at)
        {
            return (RunJobStep2Kind.NotAsked, $"Not asked for: archived on {Day(archived, at)}");
        }
        if (link.RetrievedAt is { } read && read < at)
        {
            return (RunJobStep2Kind.NotAsked, $"Not asked for: already read on {Day(read, at)}");
        }
        if (link.Status == External)
        {
            return (RunJobStep2Kind.NotAsked, "Not asked for: imported from Notion");
        }
        if (link.Status == Gone && link.LastAttemptAt is { } gone && gone < at)
        {
            return (RunJobStep2Kind.NotAsked, $"Not asked for: the page was gone on {Day(gone, at)}");
        }
        if (details.State == JournalRunState.Stopped)
        {
            return (RunJobStep2Kind.NotReached, "Not reached: the run stopped before it");
        }
        if (details.State is JournalRunState.Failed or JournalRunState.Abandoned)
        {
            return (RunJobStep2Kind.NotReached, "Not reached: the run ended with an error");
        }
        return (RunJobStep2Kind.NotReached, "Not reached");
    }

    private static (RunJobStep2Kind Kind, string Text) Tried(
        JobDetailsJournalEntry last, IReadOnlyList<JobDetailsJournalEntry> entries, string sourceJobId, int count)
    {
        var again = string.Empty;
        if (count > 1)
        {
            again = $" (on try {count})";
        }
        if (last.Status == JobResultStatus.Read)
        {
            var fit = entries.LastOrDefault(e => e.Kind == JobDetailsJournalEntryKind.Assessed && e.JobId == sourceJobId && e.Id > last.Id);
            if (fit?.Message is { Length: > 0 } scored)
            {
                if (scored.StartsWith("Not scored", StringComparison.Ordinal))
                {
                    return (RunJobStep2Kind.Read, $"Read{again} · not scored");
                }
                return (RunJobStep2Kind.Read, $"Read{again} · {scored}");
            }
            return (RunJobStep2Kind.Read, $"Read{again}");
        }
        if (last.Gone)
        {
            return (RunJobStep2Kind.Gone, "Gone: the page no longer exists");
        }
        if (last.Status == JobResultStatus.Blocked)
        {
            return (RunJobStep2Kind.Refused, $"Refused{again}: tried again on a later run");
        }
        return (RunJobStep2Kind.Failed, $"Failed: {last.Message}");
    }

    /// <summary>A date in the run's own time zone offset (some dates are stored in UTC).</summary>
    private static string Day(DateTimeOffset at, DateTimeOffset run)
    {
        return at.ToOffset(run.Offset).ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }
}
