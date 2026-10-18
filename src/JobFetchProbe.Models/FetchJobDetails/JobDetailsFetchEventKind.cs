namespace JobFetchProbe.Models.FetchJobDetails;

public enum JobDetailsFetchEventKind
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
