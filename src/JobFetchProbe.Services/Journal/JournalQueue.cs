using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Journal;

/// <summary>
/// Writes a run's journal in the background, one write at a time and in the order given, so the process that fetches
/// never waits for the journal. A write that fails is logged and skipped: the journal can lose a line, the fetch never
/// stops because of it.
/// </summary>
internal sealed class JournalQueue
{
    private readonly Channel<Func<CancellationToken, Task>> writes = Channel.CreateUnbounded<Func<CancellationToken, Task>>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly ILogger logger;
    private readonly Task worker;
    private int failures;

    public JournalQueue(ILogger logger)
    {
        this.logger = logger;
        worker = Task.Run(WorkAsync);
    }

    public void Enqueue(Func<CancellationToken, Task> write)
    {
        writes.Writer.TryWrite(write);
    }

    /// <summary>Waits until every queued write is done (or skipped). Nothing can be queued afterwards.</summary>
    public async Task CompleteAsync()
    {
        writes.Writer.TryComplete();
        await worker;
    }

    private async Task WorkAsync()
    {
        await foreach (var write in writes.Reader.ReadAllAsync())
        {
            try
            {
                await write(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Logged once, then counted: a journal that cannot be written should not flood the log.
                if (failures++ == 0)
                {
                    logger.LogWarning("Journal: a write failed and was skipped; the fetch continues. {Error}", ex.GetBaseException().Message);
                }
            }
        }
        if (failures > 1)
        {
            logger.LogWarning("Journal: {Count} writes failed in this run and were skipped.", failures);
        }
    }
}
