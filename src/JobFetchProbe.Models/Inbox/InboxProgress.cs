namespace JobFetchProbe.Models.Inbox;

/// <summary>Reported while step 1 runs: before a page is read (Result null) and after (Result set).</summary>
public sealed record InboxProgress(int Index, int Total, string File, InboxFileResult? Result);
