using JobFetchProbe.Models;

namespace JobFetchProbe.Console;

/// <summary>
/// The whole process: step 1 inbox, then (if a listing was written and Details:RunAfterInbox is true) step 2 job
/// descriptions for that listing, then one summary with the totals. Runs at start-up and with the "inbox" command.
/// </summary>
public sealed class PipelineConsole(InboxConsole inbox, DetailsConsole details)
{
    /// <param name="quietWhenEmpty">At start-up, print nothing at all if the inbox is empty.</param>
    /// <returns>True when every inbox page was archived and every listed job was read.</returns>
    public async Task<bool> RunAsync(bool quietWhenEmpty = false)
    {
        var inboxResult = await inbox.RunAsync(quietWhenEmpty);
        if (inboxResult.Files.Count == 0)
        {
            return true;
        }

        DetailsRunResult? detailsResult = null;
        if (inboxResult.SavedTo is not null && details.RunAfterInbox)
        {
            detailsResult = await details.RunAsync(inboxResult.SavedTo);
        }

        PrintSummary(inboxResult, detailsResult);
        return InboxConsole.IsComplete(inboxResult) &&
               (detailsResult is null ? !details.RunAfterInbox || inboxResult.SavedTo is null : detailsResult.Failed == 0 && detailsResult.Pending == 0);
    }

    public static void PrintSummary(InboxRunResult? inboxResult, DetailsRunResult? detailsResult)
    {
        if (inboxResult is not { Files.Count: > 0 } && detailsResult is null)
        {
            return; // nothing happened, nothing to sum up
        }

        ConsoleLog.Heading("Summary");

        if (inboxResult is { Files.Count: > 0 })
        {
            var read = inboxResult.Files.Count(f => f.Success);
            var archived = inboxResult.Files.Count(f => f.ArchivedTo is not null);
            var kept = inboxResult.Files.Count - archived;
            System.Console.WriteLine($"Inbox pages        {inboxResult.Files.Count} found, {read} read, {archived} archived, {kept} kept in the inbox");
            System.Console.WriteLine(inboxResult.SavedTo is not null && inboxResult.Batch is not null
                ? $"Jobs listed        {inboxResult.Batch.Count} unique  ->  {inboxResult.SavedTo}"
                : "Jobs listed        0 (no page could be read, so no listing was written)");
        }

        if (detailsResult is not null)
        {
            System.Console.WriteLine($"Job descriptions   {detailsResult.Total} of {detailsResult.Listed} stored  ({detailsResult.Fetched} read in this run, {detailsResult.AlreadyDone} from earlier runs)");
            System.Console.WriteLine($"                   {detailsResult.Failed} failed, {detailsResult.Pending} not tried yet");
            System.Console.WriteLine($"Requests           {detailsResult.Requests} sent in {ConsoleLog.Duration(detailsResult.Elapsed)}, circuit breaker opened {detailsResult.CircuitOpenings} time(s)");
            if (detailsResult.SavedTo is not null)
            {
                System.Console.WriteLine($"Descriptions saved {detailsResult.SavedTo}");
            }
            if (detailsResult.StoppedReason is not null)
            {
                System.Console.WriteLine($"Stopped early      {detailsResult.StoppedReason}");
            }
            else if (detailsResult.Pending == 0 && detailsResult.Failed == 0)
            {
                System.Console.WriteLine("Result             complete: every listed job has its description");
            }
        }
    }
}
