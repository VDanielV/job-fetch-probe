using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Abstractions.Journal;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Journal;

/// <summary>
/// What both run recorders share: the run's current state, the lock that keeps changes and writes in order, the
/// background write queue, the sign of life, and finishing. A recorder only says how its step's progress changes the
/// state and which entries it adds. Without a repository (the journal could not be started) nothing is written.
/// </summary>
/// <typeparam name="TState">The step's run record (<see cref="JobListingJournalRun"/>, <see cref="JobDetailsJournalRun"/>).</typeparam>
/// <typeparam name="TEntry">The step's journal entry.</typeparam>
internal sealed class RunRecorderCore<TState, TEntry>
    where TState : class
    where TEntry : class
{
    private readonly IJournalRepository? repository;
    private readonly Func<IJournalRepository, TState, TEntry?, CancellationToken, Task> save;
    private readonly TimeProvider time;
    private readonly JournalQueue? queue;
    private readonly SignOfLife? signOfLife;
    private readonly Lock gate = new();

    /// <param name="save">Writes the state and the entry, if any (for example <see cref="IJournalRepository.SaveJobDetailsRunAsync"/>).</param>
    public RunRecorderCore(
        IJournalRepository? repository,
        JournalStep step,
        long runId,
        TState state,
        Func<IJournalRepository, TState, TEntry?, CancellationToken, Task> save,
        JournalOptions options,
        TimeProvider time,
        ILogger logger)
    {
        this.repository = repository;
        this.save = save;
        this.time = time;
        RunId = runId;
        State = state;
        if (repository is not null)
        {
            queue = new JournalQueue(logger);
            signOfLife = new SignOfLife(options.SignOfLifeEvery, time, () => Touch(step));
        }
    }

    public long RunId { get; }

    /// <summary>The run as it stands. Change it only inside <see cref="Change"/>.</summary>
    public TState State { get; set; }

    /// <summary>True once the run is completed or failed; nothing is written afterwards.</summary>
    public bool Finished { get; private set; }

    /// <summary>Runs <paramref name="change"/> under the lock, with the current time, so changes and writes keep their order.</summary>
    public void Change(Action<DateTimeOffset> change)
    {
        lock (gate)
        {
            change(time.GetLocalNow());
        }
    }

    /// <summary>Queues the current state (and the entry, if any). Call it inside <see cref="Change"/>.</summary>
    public void Save(TEntry? entry)
    {
        if (repository is null || queue is null || Finished)
        {
            return;
        }
        var snapshot = State;
        queue.Enqueue(ct => save(repository, snapshot, entry, ct));
    }

    /// <summary>Stops the sign of life and waits until every queued write is done (or skipped).</summary>
    public async Task FinishAsync()
    {
        if (Finished)
        {
            return;
        }
        Finished = true;
        if (signOfLife is not null)
        {
            await signOfLife.DisposeAsync();
        }
        if (queue is not null)
        {
            await queue.CompleteAsync();
        }
    }

    private void Touch(JournalStep step)
    {
        if (repository is null || queue is null)
        {
            return;
        }
        var now = time.GetLocalNow();
        queue.Enqueue(ct => repository.TouchAsync(step, RunId, now, ct));
    }
}
