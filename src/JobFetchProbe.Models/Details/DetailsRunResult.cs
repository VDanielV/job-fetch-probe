namespace JobFetchProbe.Models.Details;

/// <summary>Totals of one run of step 2 (job descriptions).</summary>
/// <param name="Unread">Jobs in the database without a description when the step started.</param>
/// <param name="Fetched">Descriptions read and stored in this run.</param>
/// <param name="Failed">Jobs tried in this run without success (refused, error, or gone); see their status in the database.</param>
/// <param name="Pending">Jobs not tried because the run stopped; they are first in line next time.</param>
/// <param name="StillUnread">Jobs still without a description after this run (failed, refused or not tried); the next run tries them.</param>
/// <param name="TotalRead">Jobs in the database with a description, after this run.</param>
/// <param name="Requests">Requests sent in this run.</param>
/// <param name="CircuitOpenings">How many times the circuit breaker opened in this run.</param>
/// <param name="StoppedReason">Why the run stopped early (circuit breaker, or cancellation), or null.</param>
public sealed record DetailsRunResult(
    int Unread,
    int Fetched,
    int Failed,
    int Pending,
    int StillUnread,
    int TotalRead,
    int Requests,
    int CircuitOpenings,
    TimeSpan Elapsed,
    string? StoppedReason);
