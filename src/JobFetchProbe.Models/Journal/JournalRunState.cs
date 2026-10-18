namespace JobFetchProbe.Models.Journal;

/// <summary>Where a journal run stands. Every state except Running is final: readers stop there.</summary>
public enum JournalRunState
{
    /// <summary>The process is working and gives a sign of life regularly.</summary>
    Running,

    /// <summary>Finished normally.</summary>
    Completed,

    /// <summary>Ended early on purpose: the circuit breaker gave up, or the user cancelled.</summary>
    Stopped,

    /// <summary>Ended by an error, which the run's message describes.</summary>
    Failed,

    /// <summary>No sign of life for too long: the process ended without saying so (closed, killed, crashed).</summary>
    Abandoned,
}
