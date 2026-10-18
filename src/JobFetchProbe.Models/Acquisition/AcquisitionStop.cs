namespace JobFetchProbe.Models.Acquisition;

/// <summary>What happened to a request to stop an acquisition.</summary>
/// <param name="At">When the request was answered.</param>
/// <param name="Message">A sentence for the user.</param>
public sealed record AcquisitionStop(AcquisitionStopState State, DateTimeOffset At, string Message);
