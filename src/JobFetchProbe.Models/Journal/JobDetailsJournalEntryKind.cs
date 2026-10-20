namespace JobFetchProbe.Models.Journal;

/// <summary>What a step 2 journal entry records.</summary>
public enum JobDetailsJournalEntryKind
{
    /// <summary>The run starts: how many jobs it will read.</summary>
    Started,

    /// <summary>One job was tried.</summary>
    Job,

    /// <summary>The circuit opened: no requests until the time in the run's CircuitOpenUntil.</summary>
    CircuitOpened,

    /// <summary>The cooldown is over: one trial request is about to be sent.</summary>
    CircuitHalfOpen,

    /// <summary>The trial request worked: back to the normal pace.</summary>
    CircuitClosed,

    /// <summary>The run stopped before the end of its list.</summary>
    Stopped,

    /// <summary>The run ended; the message holds the totals.</summary>
    Finished,

    /// <summary>A job just read was scored (Message: "Fit 78 · Apply"), or could not be ("Not scored: …").</summary>
    Assessed,

    /// <summary>Scoring was switched off for the rest of the run after failing too many times in a row.</summary>
    AssessOff,
}
