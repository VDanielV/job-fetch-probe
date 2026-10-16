using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>One job: its details and description. ?p= remembers the table page to go back to.</summary>
public sealed class JobModel(IJobCatalog catalog) : PageModel
{
    public StoredJob? Job { get; private set; }

    public int FromPage { get; private set; } = 1;

    public async Task<IActionResult> OnGetAsync(string id, [FromQuery(Name = "p")] int page = 1, CancellationToken ct = default)
    {
        FromPage = Math.Max(1, page);
        Job = await catalog.GetAsync(id, ct);
        return Job is null ? NotFound() : Page();
    }

    /// <summary>A stored time in this computer's time zone, e.g. "2026-10-15 22:30".</summary>
    public static string When(DateTimeOffset at) => at.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
