namespace JobFetchProbe.Models.Acquisition;

/// <summary>What happened to a request to start an acquisition. Its progress is followed in the journal.</summary>
/// <param name="At">When the request was answered.</param>
/// <param name="Message">A sentence for the user.</param>
public sealed record AcquisitionStart(AcquisitionStartState State, DateTimeOffset At, string Message);
