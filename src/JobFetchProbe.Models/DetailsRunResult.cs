namespace JobFetchProbe.Models;

/// <summary>Totals of one details run.</summary>
/// <param name="Listed">Jobs in the listing file.</param>
/// <param name="AlreadyDone">Jobs already in the details file from an earlier run (skipped, no request).</param>
/// <param name="Fetched">Jobs read successfully in this run.</param>
/// <param name="Failed">Jobs that failed in this run (e.g. HTTP 404); tried again next run.</param>
/// <param name="Pending">Jobs not attempted because the run stopped.</param>
/// <param name="Total">Jobs stored in the details file (this run and earlier runs).</param>
/// <param name="Requests">Requests sent in this run.</param>
/// <param name="CircuitOpenings">How many times the circuit breaker opened in this run.</param>
/// <param name="StoppedReason">Why the run stopped early (circuit breaker, or cancellation), or null.</param>
public sealed record DetailsRunResult(
    string BatchId,
    string? SavedTo,
    int Listed,
    int AlreadyDone,
    int Fetched,
    int Failed,
    int Pending,
    int Total,
    int Requests,
    int CircuitOpenings,
    TimeSpan Elapsed,
    string? StoppedReason);

public enum DetailsEventKind
{
    /// <summary>The run starts: totals and the plan.</summary>
    Started,

    /// <summary>One job was tried.</summary>
    Job,

    /// <summary>The circuit opened: no requests for a while.</summary>
    CircuitOpened,

    /// <summary>Still waiting for the cooldown to end (sent regularly).</summary>
    Waiting,

    /// <summary>Cooldown over: one trial request is about to be sent.</summary>
    CircuitHalfOpen,

    /// <summary>The trial succeeded: back to normal.</summary>
    CircuitClosed,

    /// <summary>The run stopped before the end of the list.</summary>
    Stopped,
}

/// <summary>Reported while the details step runs, so the console can show what is happening.</summary>
public sealed record DetailsEvent(DetailsEventKind Kind, DateTimeOffset At)
{
    /// <summary>Position in the listing (1-based) of the job just tried.</summary>
    public int Index { get; init; }
    public int Listed { get; init; }
    public int AlreadyDone { get; init; }

    /// <summary>Jobs this run still has to read (at Started: all of them).</summary>
    public int ToRead { get; init; }

    public int Fetched { get; init; }
    public int Failed { get; init; }
    public JobListing? Listing { get; init; }
    public JobResultStatus? Status { get; init; }
    public string? Title { get; init; }
    public string? Message { get; init; }

    /// <summary>Waiting: time left. CircuitOpened: the cooldown. Started: the pause between requests.</summary>
    public TimeSpan? Wait { get; init; }

    /// <summary>Estimated time to finish, from the average time per job so far.</summary>
    public TimeSpan? Eta { get; init; }

    public int Openings { get; init; }
    public int StopAfterOpenings { get; init; }
}
