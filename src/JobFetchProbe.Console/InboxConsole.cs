using JobFetchProbe.Infrastructure.Inbox;
using JobFetchProbe.Models.Inbox;
using JobFetchProbe.Services.Inbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Console;

/// <summary>
/// Step 1 on the console: reads every saved search page in the input folder, stores their job links in the
/// database, and archives the pages read. Prints each page as it goes.
/// </summary>
public sealed class InboxConsole(IServiceScopeFactory scopes, IOptions<InboxSettings> settings)
{
    /// <summary>True when every page in the input folder was read and archived (or there was none).</summary>
    public static bool IsComplete(InboxRunResult result) => result.Files.All(f => f.Success && f.ArchivedTo is not null);

    public async Task<InboxRunResult> RunAsync()
    {
        var folder = Path.GetFullPath(settings.Value.Folder);
        ConsoleLog.Heading("Step 1: saved search pages → job links");

        var progress = new SyncProgress<InboxProgress>(p =>
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
        });

        using var scope = scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInboxService>().RunAsync(progress);

        if (result.Files.Count == 0)
        {
            ConsoleLog.Info($"No saved pages in {folder}. Nothing new to add.");
            return result;
        }
        if (result.ListingId is null || result.Batch is null)
        {
            ConsoleLog.Warn("No page could be read, so nothing was stored. The pages stay in the input folder.");
            return result;
        }

        ConsoleLog.Good($"Stored {result.Batch.Count} unique job links as listing {result.ListingId}.");
        var archived = result.Files.Count(f => f.ArchivedTo is not null);
        ConsoleLog.Info($"Archived {archived} page(s) to {Path.GetFullPath(settings.Value.ArchiveFolder)}");
        foreach (var f in result.Files.Where(f => f.Success && f.ArchivedTo is null))
        {
            ConsoleLog.Warn($"{f.File}: {f.Message}");
        }
        return result;
    }
}
