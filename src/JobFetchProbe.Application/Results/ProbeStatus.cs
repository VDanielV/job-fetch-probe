namespace JobFetchProbe.Application.Results;

/// <summary>How a run ended. The console maps this to an exit code.</summary>
public enum ProbeStatus
{
    /// <summary>Finished and wrote a report.</summary>
    Completed,

    /// <summary>Stopped on a block, challenge, auth wall or disallow. A completed observation, not a failure.</summary>
    Stopped,

    /// <summary>Transport failure or timeout. Report still written.</summary>
    TransportFailed,
}
