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
/// <param name="Query">The search text (company or title), as given; null without a search.</param>
/// <param name="Terms">With a search: the words and phrases looked for, to highlight them. Null without one.</param>
public sealed record ApplicationPage(
    IReadOnlyList<ApplicationSummary> Applications,
    int Page,
    int PageSize,
    int Total,
    int Open,
    int Closed,
    int Flagged,
    IReadOnlyList<ApplicationStageCount> Stages,
    string? Query = null,
    IReadOnlyList<string>? Terms = null)
{
    public int TotalPages
    {
        get
        {
            return Math.Max(1, (Total + PageSize - 1) / PageSize);
        }
    }
}
