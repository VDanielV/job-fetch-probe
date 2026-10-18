using JobFetchProbe.Models.Acquisition;
using JobFetchProbe.Models.Journal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Acquisition;

/// <summary>
/// Runs one acquisition at a time on a background task. As a hosted service it is told when the application stops:
/// the acquisition is then cancelled (step 2 stops after the current job, like Ctrl+C on the console) and awaited, so
/// nothing is left half-written.
/// </summary>
public sealed class AcquisitionRunner(
    IAcquisitionService acquisition,
    ILoggerFactory loggers,
    TimeProvider time) : IAcquisitionRunner, IHostedService, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stopping = new();
    private readonly ILogger logger = loggers.CreateLogger<AcquisitionRunner>();
    private Task? current;

    public async Task<AcquisitionStart> TryStartAsync(JournalStarter startedBy, CancellationToken ct = default)
    {
        // One check-and-start at a time, so two requests arriving together cannot both start a run.
        await gate.WaitAsync(ct);
        try
        {
            if (stopping.IsCancellationRequested)
            {
                return Answer(AcquisitionStartState.AlreadyRunning, "The application is stopping; no new acquisition is started.");
            }
            if (current is { IsCompleted: false })
            {
                return Answer(AcquisitionStartState.AlreadyRunning, "An acquisition started here is still running. Follow it in the journal.");
            }
            if (await acquisition.IsRunningAsync(ct))
            {
                return Answer(
                    AcquisitionStartState.AlreadyRunning,
                    "An acquisition is already running (started by the console app, or another API). Follow it in the journal. " +
                    "A run that ended without finishing counts as running until it has shown no sign of life for 2 minutes.");
            }

            current = Task.Run(() => RunAsync(startedBy));
            return Answer(AcquisitionStartState.Started, "Acquisition started: step 1, then step 2. Follow it in the journal.");
        }
        finally
        {
            gate.Release();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>The application is stopping: cancels the acquisition and waits for it (or for the host's shutdown timeout).</summary>
    public async Task StopAsync(CancellationToken ct)
    {
        await stopping.CancelAsync();
        if (current is { IsCompleted: false } running)
        {
            logger.LogWarning("The application is stopping: the acquisition stops after the current job.");
            await running.WaitAsync(ct);
        }
    }

    public void Dispose()
    {
        stopping.Dispose();
        gate.Dispose();
    }

    private async Task RunAsync(JournalStarter startedBy)
    {
        var observer = new LoggingAcquisitionObserver(loggers.CreateLogger<LoggingAcquisitionObserver>());
        try
        {
            var result = await acquisition.RunAsync(startedBy, observer, stopping.Token);
            if (result.NothingLeft)
            {
                logger.LogInformation("Acquisition finished: nothing left to do.");
            }
            else
            {
                logger.LogInformation("Acquisition finished: something is left for the next run.");
            }
        }
        catch (Exception ex)
        {
            // Recorded as Failed in the journal by the acquisition; here it only goes to the log.
            logger.LogError(ex, "Acquisition failed: {Error}", ex.GetBaseException().Message);
        }
    }

    private AcquisitionStart Answer(AcquisitionStartState state, string message)
    {
        return new AcquisitionStart(state, time.GetLocalNow(), message);
    }
}
