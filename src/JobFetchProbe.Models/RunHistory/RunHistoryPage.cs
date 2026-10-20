namespace JobFetchProbe.Models.RunHistory;

/// <summary>One page of past acquisitions, newest first.</summary>
/// <param name="Total">Acquisitions in the list (empty ones only when asked for).</param>
/// <param name="EmptyRuns">Acquisitions that did nothing: hidden unless asked for.</param>
public sealed record RunHistoryPage(IReadOnlyList<RunHistoryLine> Runs, int Page, int PageSize, int Total, int EmptyRuns);
