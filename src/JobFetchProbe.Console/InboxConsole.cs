using JobFetchProbe.Infrastructure.Inbox;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Inbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Console;

/// <summary>
/// The inbox step on the console: reads every saved search page in Inbox:Folder, writes one
/// &lt;yyyyMMdd_HHmmss&gt;.json with all their jobs, and archives the pages read. Prints each page as it goes.
/// </summary>
public sealed class InboxConsole(IServiceScopeFactory scopes, IOptions<InboxSettings> settings)
{
    public const string Command = "inbox";

    public bool ProcessOnStart => settings.Value.ProcessOnStart;

    public static bool IsInbox(string input) => input.Equals(Command, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when every page in the inbox was read and archived (or the inbox was empty).</summary>
    public static bool IsComplete(InboxRunResult result) =>
        result.Error is null && result.Files.All(f => f.Success && f.ArchivedTo is not null);

    /// <param name="quietWhenEmpty">At start-up, say nothing if the inbox is empty.</param>
    public async Task<InboxRunResult> RunAsync(bool quietWhenEmpty = false)
    {
        var folder = Path.GetFullPath(settings.Value.Folder);
        var headingShown = false;

        void ShowHeading(int total)
        {
            if (headingShown)
            {
                return;
            }
            headingShown = true;
            ConsoleLog.Heading("Step 1: inbox");
            ConsoleLog.Info($"Reading {total} saved page(s) from {folder}");
        }

        var progress = new SyncProgress<InboxProgress>(p =>
        {
            ShowHeading(p.Total);
            if (p.Result is null)
            {
                return; // the result line follows immediately; reading a file takes well under a second
            }
            var label = $"[{p.Index}/{p.Total}] {p.File}";
            if (p.Result.Success)
            {
                ConsoleLog.Good($"{label}: {p.Result.Jobs} jobs ({p.Result.NewJobs} new)");
            }
            else
            {
                ConsoleLog.Warn($"{label}: kept in the inbox. {p.Result.Message}");
            }
        });

        using var scope = scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInboxService>().RunAsync(progress);

        if (result.Files.Count == 0)
        {
            if (!quietWhenEmpty)
            {
                ConsoleLog.Info($"Inbox {folder}: no saved pages to read.");
            }
            return result;
        }

        if (result.Error is not null)
        {
            ConsoleLog.Bad($"Nothing was archived: {result.Error}");
            return result;
        }
        if (result.SavedTo is null || result.Batch is null)
        {
            ConsoleLog.Warn("No page could be read, so no listing was written. The pages stay in the inbox.");
            return result;
        }

        ConsoleLog.Good($"Listing written: {result.Batch.Count} unique jobs -> {result.SavedTo}");
        var archived = result.Files.Count(f => f.ArchivedTo is not null);
        ConsoleLog.Info($"Archived {archived} page(s) to {Path.GetFullPath(settings.Value.ArchiveFolder)}");
        foreach (var f in result.Files.Where(f => f.Success && f.ArchivedTo is null))
        {
            ConsoleLog.Warn($"{f.File}: {f.Message}");
        }
        return result;
    }
}

/// <summary>Reports on the calling thread, so lines print in order (Progress&lt;T&gt; would post to the thread pool).</summary>
internal sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
