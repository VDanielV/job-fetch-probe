namespace JobFetchProbe.Models.Acquisition;

/// <summary>The answer to a request to stop an acquisition.</summary>
public enum AcquisitionStopState
{
    /// <summary>The acquisition stops after the current page or job (at once during a pause or a cooldown).</summary>
    Stopping,

    /// <summary>Nothing to stop here: no acquisition is running in this process.</summary>
    NotRunning,
}
