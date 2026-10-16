namespace JobFetchProbe.Models.Inbox;

/// <summary>Outcome of one run of step 1 (inbox).</summary>
/// <param name="Batch">The merged jobs, or null when no page was read successfully.</param>
/// <param name="ListingId">The listing's ID in the database, or null when nothing was stored.</param>
public sealed record InboxRunResult(
    IReadOnlyList<InboxFileResult> Files,
    JobSearchBatch? Batch,
    string? ListingId)
{
    public static InboxRunResult Empty { get; } = new([], null, null);
}
