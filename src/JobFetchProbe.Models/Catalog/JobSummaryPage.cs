using JobFetchProbe.Models.Filter;

namespace JobFetchProbe.Models.Catalog;

/// <summary>One page of the jobs table.</summary>
/// <param name="Page">1-based page number actually returned (clamped to the existing pages).</param>
/// <param name="Total">Number of jobs listed: the jobs to review, plus the archived ones when they were asked for; with a
/// text filter, the jobs that match.</param>
/// <param name="ArchivedCount">Number of archived jobs in the database, listed or not.</param>
/// <param name="Terms">With a text filter: the words and phrases looked for, to highlight them. Null without one.</param>
/// <param name="UnfilteredTotal">With any filter on (text, place or work mode): the number of jobs listed without them. Null without.</param>
/// <param name="Places">The city chips, with their counts.</param>
/// <param name="Modes">The work-mode chips, with their counts.</param>
public sealed record JobSummaryPage(
    IReadOnlyList<JobSummary> Jobs, int Page, int PageSize, int Total, int ArchivedCount = 0,
    IReadOnlyList<string>? Terms = null, int? UnfilteredTotal = null,
    IReadOnlyList<FilterChip>? Places = null, IReadOnlyList<FilterChip>? Modes = null)
{
    public int TotalPages => Math.Max(1, (Total + PageSize - 1) / PageSize);
}
