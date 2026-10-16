using JobFetchProbe.Infrastructure.Details;
using JobFetchProbe.Models.Details;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Services.Details;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Console;

/// <summary>
/// Step 2 on the console: reads every job in the database without a description, one by one, and stores each
/// result. Prints the plan, each job with progress and time left, every circuit breaker change, and a countdown
/// while the circuit is open. Ctrl+C stops after the current job; the next run continues from there.
/// </summary>
public sealed class DetailsConsole(IServiceScopeFactory scopes, IOptions<DetailsSettings> settings)
{
    public async Task<DetailsRunResult> RunAsync()
    {
        ConsoleLog.Heading("Step 2: job links → job descriptions");

        var delay = TimeSpan.FromSeconds(settings.Value.DelaySeconds);
        var breaker = settings.Value.CircuitBreaker.ToOptions();

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true; // keep the process alive so the summary is printed
            if (!cts.IsCancellationRequested)
            {
                ConsoleLog.Warn("Ctrl+C: stopping after the current job, everything read so far is kept…");
                cts.Cancel();
            }
        };
        System.Console.CancelKeyPress += onCancel;

        try
        {
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDetailsService>();
            var progress = new SyncProgress<DetailsEvent>(e => Print(e, breaker));
            return await service.RunAsync(delay, breaker, progress, cts.Token);
        }
        finally
        {
            System.Console.CancelKeyPress -= onCancel;
        }
    }

    private static void Print(DetailsEvent e, CircuitBreakerOptions breaker)
    {
        switch (e.Kind)
        {
            case DetailsEventKind.Started:
                if (e.ToRead == 0)
                {
                    ConsoleLog.Info("Every job in the database already has its description. Nothing to read.");
                }
                else
                {
                    var estimate = TimeSpan.FromSeconds(e.ToRead * ((e.Wait ?? TimeSpan.Zero).TotalSeconds + 1));
                    ConsoleLog.Info($"{e.ToRead} job(s) without a description in the database.");
                    ConsoleLog.Info($"One request every {ConsoleLog.Duration(e.Wait ?? TimeSpan.Zero)}, about {ConsoleLog.Duration(estimate)} in total if the site keeps answering.");
                    ConsoleLog.Info($"Circuit breaker: opens on a refusal or {breaker.FailureThreshold} failures in a row, " +
                                    $"waits {ConsoleLog.Duration(breaker.Cooldown)} (doubling, max {ConsoleLog.Duration(breaker.MaxCooldown)}), " +
                                    $"stops after {breaker.StopAfterOpenings} opening(s). Ctrl+C stops after the current job.");
                }
                break;

            case DetailsEventKind.Job:
                var percent = e.Total > 0 ? 100.0 * e.Index / e.Total : 100;
                var eta = e.Eta is { } t && e.ToRead > 0 ? $", ~{ConsoleLog.Duration(t)} left" : string.Empty;
                var head = $"[{e.Index,4}/{e.Total}] {percent,3:0}%";
                var tail = $"(ok {e.Fetched}, failed {e.Failed}, {e.ToRead} to go{eta})";
                switch (e.Status)
                {
                    case JobResultStatus.Read:
                        ConsoleLog.Good($"{head}  ok       {e.Job?.JobId}  {Short(e.Title ?? e.Job?.Title, 50)} · {Short(e.Job?.Company, 30)}  {tail}");
                        break;
                    case JobResultStatus.Blocked:
                        ConsoleLog.Bad($"{head}  REFUSED  {e.Job?.JobId}  {tail}");
                        break;
                    case JobResultStatus.Failed when e.Gone:
                        ConsoleLog.Warn($"{head}  gone     {e.Job?.JobId}  the job page no longer exists; not tried again  {tail}");
                        break;
                    default:
                        ConsoleLog.Warn($"{head}  failed   {e.Job?.JobId}  {Short(e.Message, 60)}  {tail}");
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
                ConsoleLog.Info($"{e.ToRead} job(s) not tried in this run. The next run starts with them; jobs already stored are not asked again.");
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
