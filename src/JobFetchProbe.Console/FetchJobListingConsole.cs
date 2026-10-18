using JobFetchProbe.Models.FetchJobListing;

namespace JobFetchProbe.Console;

/// <summary>
/// Prints step 1 (saved search pages in the input folder → job links in the database, pages archived): the heading,
/// each page as it is read, and what was stored and archived.
/// </summary>
public sealed class FetchJobListingConsole(ConsolePaths paths)
{
    public void Starting()
    {
        ConsoleLog.Heading("Step 1: saved search pages → job links");
    }

    public void Progress(JobListingFetchProgress p)
    {
        if (p.Result is null)
        {
            return; // the result line follows immediately; reading a file takes well under a second
        }
        var label = $"[{p.Index}/{p.Total}] {p.File}";
        if (p.Result.Success)
        {
            ConsoleLog.Good($"{label}: {p.Result.Jobs} jobs ({p.Result.NewJobs} new in this run)");
        }
        else
        {
            ConsoleLog.Warn($"{label}: kept in the input folder. {p.Result.Message}");
        }
    }

    public void Finished(JobListingFetchResult result)
    {
        if (result.Files.Count == 0)
        {
            ConsoleLog.Info($"No saved pages in {paths.Input}. Nothing new to add.");
            return;
        }
        if (result.ListingId is null || result.Batch is null)
        {
            ConsoleLog.Warn("No page could be read, so nothing was stored. The pages stay in the input folder.");
            return;
        }

        ConsoleLog.Good($"Stored {result.Batch.Count} unique job links as listing {result.ListingId}.");
        var archived = result.Files.Count(f => f.ArchivedTo is not null);
        ConsoleLog.Info($"Archived {archived} page(s) to {paths.Archive}");
        foreach (var f in result.Files.Where(f => f.Success && f.ArchivedTo is null))
        {
            ConsoleLog.Warn($"{f.File}: {f.Message}");
        }
    }

    /// <summary>The line printed under each step's heading.</summary>
    public static string JournalLine(long runId)
    {
        if (runId > 0)
        {
            return $"Journal: run {runId}";
        }
        return "Journal: not recorded this time (see the warning above); the run continues.";
    }
}
