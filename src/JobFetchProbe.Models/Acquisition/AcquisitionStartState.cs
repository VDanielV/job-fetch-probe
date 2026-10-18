namespace JobFetchProbe.Models.Acquisition;

/// <summary>The answer to a request to start an acquisition.</summary>
public enum AcquisitionStartState
{
    /// <summary>The acquisition started and runs in the background.</summary>
    Started,

    /// <summary>Not started: an acquisition is already running (in this process, or in another one such as the console app).</summary>
    AlreadyRunning,
}
