namespace JobFetchProbe.Models.Catalog;

/// <summary>One page of the jobs table.</summary>
/// <param name="Page">1-based page number actually returned (clamped to the existing pages).</param>
/// <param name="Total">Number of jobs in the database.</param>
public sealed record JobSummaryPage(IReadOnlyList<JobSummary> Jobs, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (Total + PageSize - 1) / PageSize);
}
