using System.Diagnostics;

namespace JobFetchProbe.Services.FetchJobDetails;

/// <summary>
/// Step 2's clock: the time since the run started, and the "time left" estimate. The estimate counts only the time
/// spent requesting and pausing between requests, not circuit breaker cooldowns, which say nothing about the pace.
/// </summary>
internal sealed class TimeLeftEstimate
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private TimeSpan active = TimeSpan.Zero;

    /// <summary>Time since the run started.</summary>
    public TimeSpan Elapsed
    {
        get
        {
            return clock.Elapsed;
        }
    }

    /// <summary>Adds the time from <paramref name="started"/> (an earlier <see cref="Elapsed"/>) until now as active time.</summary>
    public void AddActiveSince(TimeSpan started)
    {
        active += clock.Elapsed - started;
    }

    /// <summary>Active time per request so far, times the jobs left; null before the first request.</summary>
    public TimeSpan? For(int requests, int left)
    {
        if (requests <= 0)
        {
            return null;
        }
        return TimeSpan.FromTicks(active.Ticks / requests * left);
    }
}
