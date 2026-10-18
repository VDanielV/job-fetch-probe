using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.Jobs;

namespace JobFetchProbe.Console;

/// <summary>
/// Prints step 2 (every job in the database without a description → its description): the heading, the plan, each
/// job with progress and time left, every circuit breaker change, and a countdown while the circuit is open.
/// </summary>
public sealed class FetchJobDetailsConsole
{
    public void Starting()
    {
        ConsoleLog.Heading("Step 2: job links → job descriptions");
    }

    public void Print(JobDetailsFetchEvent e)
    {
        switch (e.Kind)
        {
            case JobDetailsFetchEventKind.Started:
                if (e.ToRead == 0)
                {
                    ConsoleLog.Info("Every job in the database already has its description. Nothing to read.");
                }
                else
                {
                    var estimate = TimeSpan.FromSeconds(e.ToRead * ((e.Wait ?? TimeSpan.Zero).TotalSeconds + 1));
                    ConsoleLog.Info($"{e.ToRead} job(s) without a description in the database.");
                    ConsoleLog.Info($"One request every {ConsoleLog.Duration(e.Wait ?? TimeSpan.Zero)}, about {ConsoleLog.Duration(estimate)} in total if the site keeps answering.");
                    ConsoleLog.Info($"Circuit breaker: opens on a refusal or {e.FailureThreshold} failures in a row, " +
                                    $"waits {ConsoleLog.Duration(e.Cooldown ?? TimeSpan.Zero)} (doubling, max {ConsoleLog.Duration(e.MaxCooldown ?? TimeSpan.Zero)}), " +
                                    $"stops after {e.StopAfterOpenings} opening(s). Ctrl+C stops after the current job.");
                }
                break;

            case JobDetailsFetchEventKind.Job:
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

            case JobDetailsFetchEventKind.CircuitOpened:
                ConsoleLog.Bad($"Circuit OPEN ({e.Openings} of {e.StopAfterOpenings}): {e.Message}.");
                ConsoleLog.Warn($"No requests for {ConsoleLog.Duration(e.Wait ?? TimeSpan.Zero)}, then one trial request. " +
                                (e.Openings + 1 >= e.StopAfterOpenings
                                    ? "If that is refused too, the run stops."
                                    : "Ctrl+C stops now and keeps the progress."));
                break;

            case JobDetailsFetchEventKind.Waiting:
                ConsoleLog.Info($"  … circuit open, trial request in {ConsoleLog.Duration(e.Wait ?? TimeSpan.Zero)}");
                break;

            case JobDetailsFetchEventKind.CircuitHalfOpen:
                ConsoleLog.Warn("Circuit HALF-OPEN: sending one trial request.");
                break;

            case JobDetailsFetchEventKind.CircuitClosed:
                ConsoleLog.Good("Circuit CLOSED: the trial request worked, continuing at the normal pace.");
                break;

            case JobDetailsFetchEventKind.Stopped:
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
