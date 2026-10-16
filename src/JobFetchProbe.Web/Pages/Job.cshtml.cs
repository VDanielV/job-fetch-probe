using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>Old address of a job (/jobs/{id}): the job now opens in the details pane of the overview.</summary>
public sealed class JobModel : PageModel
{
    public IActionResult OnGet(string id, [FromQuery(Name = "p")] int page = 1)
    {
        return RedirectToPage("/Index", new { p = Math.Max(1, page), job = id });
    }
}
