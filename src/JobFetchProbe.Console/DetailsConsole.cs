using JobFetchProbe.Infrastructure.Details;
using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Details;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Console;

/// <summary>
/// The details step on the console: reads every job of a listing, one by one, and stores all their descriptions
/// in one file. Prints the plan, each job with progress and time left, every circuit breaker change, and a
/// countdown while the circuit is open. Ctrl+C stops after the current job; the next run continues from there.
/// </summary>
public sealed class DetailsConsole(IServiceScopeFactory scopes, IOptions<DetailsSettings> settings, IListingStore listings)
{
    public const string Command = "details";

    public bool RunAfterInbox => settings.Value.RunAfterInbox;

    public static bool IsDetails(string input) =>
        input.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith(Command + " ", StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs "details", "details &lt;batch ID&gt;" or "details &lt;listing.json&gt;" as typed at the prompt.</summary>
    public Task<DetailsRunResult?> HandleLineAsync(string line) =>
        RunAsync(line.Length > Command.Length ? line[Command.Length..].Trim().Trim('"') : null);

    /// <param name="listingFile">Listing to read (file, batch ID or stored location); null means the newest one.</param>
    public async Task<DetailsRunResult?> RunAsync(string? listingFile)
    {
        ConsoleLog.Heading("Step 2: job descriptions");

        var typed = listingFile;
        listingFile = string.IsNullOrWhiteSpace(typed) ? listings.FindLatest() : listings.Resolve(typed);
        if (listingFile is null)
        {
            ConsoleLog.Warn(string.IsNullOrWhiteSpace(typed)
                ? "No listing found. Run the inbox step first, or give one: details <yyyyMMdd_HHmmss>"
                : $"Listing not found: {typed}");
            return null;
        }

        var delay = TimeSpan.FromSeconds(settings.Value.DelaySeconds);
        var breaker = settings.Value.CircuitBreaker.ToOptions();

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true; // keep the process alive so progress is saved
            if (!cts.IsCancellationRequested)
            {
                ConsoleLog.Warn("Ctrl+C: stopping after the current job, progress is kept…");
                cts.Cancel();
            }
        };
        System.Console.CancelKeyPress += onCancel;

        try
        {
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDetailsService>();
            var progress = new SyncProgress<DetailsEvent>(e => Print(e, listingFile, breaker));
            return await service.RunAsync(listingFile, delay, breaker, progress, cts.Token);
        }
        catch (InvalidDataException ex)
        {
            ConsoleLog.Bad(ex.Message);
            return null;
        }
        finally
        {
            System.Console.CancelKeyPress -= onCancel;
        }
    }

    private static void Print(DetailsEvent e, string listingFile, CircuitBreakerOptions breaker)
    {
        switch (e.Kind)
        {
            case DetailsEventKind.Started:
                ConsoleLog.Info($"Listing {Path.GetFileName(listingFile)}: {e.Listed} jobs, {e.AlreadyDone} already stored, {e.ToRead} to read.");
                if (e.ToRead > 0)
                {
                    var estimate = TimeSpan.FromSeconds(e.ToRead * ((e.Wait ?? TimeSpan.Zero).TotalSeconds + 1));
                    ConsoleLog.Info($"One request every {ConsoleLog.Duration(e.Wait ?? TimeSpan.Zero)}, about {ConsoleLog.Duration(estimate)} in total if the site keeps answering.");
                    ConsoleLog.Info($"Circuit breaker: opens on a refusal or {breaker.FailureThreshold} failures in a row, " +
                                    $"waits {ConsoleLog.Duration(breaker.Cooldown)} (doubling, max {ConsoleLog.Duration(breaker.MaxCooldown)}), " +
                                    $"stops after {breaker.StopAfterOpenings} opening(s). Ctrl+C stops after the current job.");
                }
                break;

            case DetailsEventKind.Job:
                var percent = e.Listed > 0 ? 100.0 * e.Index / e.Listed : 100;
                var eta = e.Eta is { } t && e.ToRead > 0 ? $", ~{ConsoleLog.Duration(t)} left" : string.Empty;
                var head = $"[{e.Index,4}/{e.Listed}] {percent,3:0}%";
                var tail = $"(ok {e.Fetched}, failed {e.Failed}, {e.ToRead} to go{eta})";
                switch (e.Status)
                {
                    case JobResultStatus.Read:
                        ConsoleLog.Good($"{head}  ok       {e.Listing?.JobId}  {Short(e.Title ?? e.Listing?.Title, 50)} · {Short(e.Listing?.Company, 30)}  {tail}");
                        break;
                    case JobResultStatus.Blocked:
                        ConsoleLog.Bad($"{head}  REFUSED  {e.Listing?.JobId}  {tail}");
                        break;
                    default:
                        ConsoleLog.Warn($"{head}  failed   {e.Listing?.JobId}  {Short(e.Message, 60)}  {tail}");
                        break;
                }
                break;

            case DetailsEventKind.CircuitOpened:
                ConsoleLog.Bad($"Circuit OPEN ({e.Openings} of {e.StopAfterOpenings}): {e.Message}.");
                ConsoleLog.Warn($"No requests for {ConsoleLog.Duration(e.Wait ?? TimeSpan.Zero)}, then one trial request. " +
                                (e.Openings + 1 >= e.StopAfterOpenings
                                    ? "If that is refused too, the run stops."
                                    : "Ctrl+C stops now and keeps the progress."));
                break;

            case DetailsEventKind.Waiting:
                ConsoleLog.Info($"  … circuit open, trial request in {ConsoleLog.Duration(e.Wait ?? TimeSpan.Zero)}");
                break;

            case DetailsEventKind.CircuitHalfOpen:
                ConsoleLog.Warn("Circuit HALF-OPEN: sending one trial request.");
                break;

            case DetailsEventKind.CircuitClosed:
                ConsoleLog.Good("Circuit CLOSED: the trial request worked, continuing at the normal pace.");
                break;

            case DetailsEventKind.Stopped:
                ConsoleLog.Bad($"Stopped: {e.Message}");
                ConsoleLog.Info($"{e.ToRead} job(s) not tried yet. Run 'details' later to continue; jobs already stored are skipped.");
                break;
        }
    }

    private static string Short(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "-";
        }
        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
