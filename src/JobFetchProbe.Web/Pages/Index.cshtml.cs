using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// Overview: all jobs, newest first, one page at a time (?p=2), and the selected job in the details pane (?job=…).
/// Both come from the API: GET api/JobCatalog?page=&amp;pageSize= and GET api/JobCatalog/{jobId}.
/// </summary>
public sealed class IndexModel(IJobCatalogClient api) : PageModel
{
    public const int PageSize = 25;

    /// <summary>Null when the jobs cannot be shown; <see cref="Unavailable"/> then says why.</summary>
    public JobSummaryPage? Jobs { get; private set; }

    /// <summary>Why the jobs cannot be shown: no database yet (the API's message), or the API is not reachable.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>The job requested with ?job=, or null.</summary>
    public string? SelectedId { get; private set; }

    /// <summary>The job shown in the details pane; null when none is selected or it does not exist.</summary>
    public StoredJob? Selected { get; private set; }

    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int page = 1,
        [FromQuery(Name = "job")] string? job = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(job))
        {
            SelectedId = job.Trim();
        }

        try
        {
            // The API rejects a page below 1; a page past the end it corrects itself (the result says which page).
            Jobs = await api.GetPageAsync(Math.Max(1, page), PageSize, ct);
            if (SelectedId is not null)
            {
                Selected = await api.GetJobAsync(SelectedId, ct);
                if (Selected is null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                }
            }
        }
        catch (ApiUnavailableException ex)
        {
            Unavailable = ex.Message;
        }
        catch (ApiErrorException ex)
        {
            Unavailable = "The API could not read the jobs: " + ex.Message;
        }
    }

    /// <summary>The page numbers to show: the first, the last, and the current one with its neighbours; null marks a gap.</summary>
    public IReadOnlyList<int?> PageNumbers()
    {
        var result = new List<int?>();
        if (Jobs is null)
        {
            return result;
        }

        var last = Jobs.TotalPages;
        var current = Jobs.Page;
        int? previous = null;
        for (var n = 1; n <= last; n++)
        {
            if (n == 1 || n == last || Math.Abs(n - current) <= 1)
            {
                if (previous is not null && n - previous > 1)
                {
                    result.Add(null);
                }
                result.Add(n);
                previous = n;
            }
        }
        return result;
    }

    /// <summary>A stored time in this computer's time zone, e.g. "2026-10-15 22:30".</summary>
    public static string When(DateTimeOffset at) => at.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
