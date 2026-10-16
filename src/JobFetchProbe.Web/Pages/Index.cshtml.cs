using JobFetchProbe.Infrastructure.Database;
using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Web.Pages;

/// <summary>All jobs, newest first, one page at a time (?p=2).</summary>
public sealed class IndexModel(IJobCatalog catalog, IOptions<DatabaseSettings> database) : PageModel
{
    public const int PageSize = 25;

    /// <summary>Null when the database file does not exist yet.</summary>
    public JobSummaryPage? Jobs { get; private set; }

    public string DatabasePath => database.Value.FullPath;

    public async Task OnGetAsync([FromQuery(Name = "p")] int page = 1, CancellationToken ct = default)
    {
        if (System.IO.File.Exists(DatabasePath))
        {
            Jobs = await catalog.GetPageAsync(page, PageSize, ct);
        }
    }
}
