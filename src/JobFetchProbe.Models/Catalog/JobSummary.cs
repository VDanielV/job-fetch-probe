using JobFetchProbe.Models.Archive;
using JobFetchProbe.Models.Filter;
using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Models.Catalog;

/// <summary>One row of the jobs table in the web app.</summary>
/// <param name="Archive">When and why the job was archived; null for a job to review.</param>
/// <param name="Snippet">With a text filter: the part of the description that matched, when the title did not.</param>
/// <param name="Place">The city read from <paramref name="Location"/> ("Spain, no city" when it names only the country).</param>
/// <param name="WorkMode">Remote, hybrid or on-site, as the location text says.</param>
/// <param name="Fit">The latest fit assessment; null when the job was never assessed.</param>
public sealed record JobSummary(
    string JobId, string? Title, string? Company, string? Location, string? Posted, string Status, JobArchiveInfo? Archive = null,
    string? Snippet = null, string? Place = null, WorkMode? WorkMode = null, FitScore? Fit = null);
