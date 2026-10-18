namespace JobFetchProbe.Services.Journal;

/// <summary>Calls <c>beat</c> regularly while a run is open, so readers can tell a working process from a dead one.</summary>
internal sealed class SignOfLife : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly PeriodicTimer timer;
    private readonly Task loop;

    public SignOfLife(TimeSpan every, TimeProvider time, Action beat)
    {
        timer = new PeriodicTimer(every, time);
        loop = BeatAsync(beat);
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        await loop;
        timer.Dispose();
        stop.Dispose();
    }

    private async Task BeatAsync(Action beat)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                beat();
            }
        }
        catch (OperationCanceledException)
        {
            // The run ended.
        }
    }
}
