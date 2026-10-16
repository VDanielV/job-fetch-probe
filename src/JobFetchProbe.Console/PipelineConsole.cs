using JobFetchProbe.Data;
using JobFetchProbe.Models.Details;
using JobFetchProbe.Models.Inbox;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Console;

/// <summary>
/// The whole run: step 1 (saved search pages → job links in the database, pages archived), then step 2 (every job
/// without a description → its description in the database), then one summary with the totals.
/// </summary>
public sealed class PipelineConsole(InboxConsole inbox, DetailsConsole details, IOptions<DatabaseSettings> database)
{
    /// <returns>0 when nothing is left to do (no page kept in the input folder, every job read or gone); 1 otherwise.</returns>
    public async Task<int> RunAsync()
    {
        var inboxResult = await inbox.RunAsync();
        var detailsResult = await details.RunAsync();
        PrintSummary(inboxResult, detailsResult);
        return InboxConsole.IsComplete(inboxResult) && detailsResult.StillUnread == 0 ? 0 : 1;
    }

    private void PrintSummary(InboxRunResult step1, DetailsRunResult step2)
    {
        ConsoleLog.Heading("Summary");

        if (step1.Files.Count == 0)
        {
            Line("Input pages", "none");
        }
        else
        {
            var archived = step1.Files.Count(f => f.ArchivedTo is not null);
            var kept = step1.Files.Count - archived;
            Line("Input pages", $"{step1.Files.Count} found, {archived} read and archived, {kept} kept in the input folder");
            Line("Job links", step1.ListingId is not null && step1.Batch is not null
                ? $"{step1.Batch.Count} stored as listing {step1.ListingId}"
                : "none stored (no page could be read)");
        }

        Line("Descriptions", $"{step2.Fetched} read in this run, {step2.Failed} failed, {step2.Pending} not tried");
        Line(string.Empty, $"{step2.TotalRead} stored in total; {step2.StillUnread} still to read (tried again on the next run)");
        Line("Requests", $"{step2.Requests} sent in {ConsoleLog.Duration(step2.Elapsed)}, circuit breaker opened {step2.CircuitOpenings} time(s)");
        Line("Database", database.Value.FullPath);

        if (step2.StoppedReason is not null)
        {
            Line("Stopped early", step2.StoppedReason);
        }
        else if (step2.StillUnread == 0 && InboxConsole.IsComplete(step1))
        {
            Line("Result", "complete: every job has its description");
        }
    }

    private static void Line(string label, string text) => System.Console.WriteLine($"{label,-15}  {text}");
}
