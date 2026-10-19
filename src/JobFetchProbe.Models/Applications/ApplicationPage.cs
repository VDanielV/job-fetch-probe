namespace JobFetchProbe.Models.Applications;

/// <summary>
/// One page of the Applications list. Open applications come flagged first (No answer, then Follow up), then newest
/// applied first; closed ones most recently closed first.
/// </summary>
/// <param name="Total">Applications in this list (after the stage or flag filter).</param>
/// <param name="Open">All open applications.</param>
/// <param name="Closed">All closed applications.</param>
/// <param name="Flagged">Open applications with a follow-up flag.</param>
/// <param name="Stages">The count for every stage.</param>
public sealed record ApplicationPage(
    IReadOnlyList<ApplicationSummary> Applications,
    int Page,
    int PageSize,
    int Total,
    int Open,
    int Closed,
    int Flagged,
    IReadOnlyList<ApplicationStageCount> Stages)
{
    public int TotalPages
    {
        get
        {
            return Math.Max(1, (Total + PageSize - 1) / PageSize);
        }
    }
}
