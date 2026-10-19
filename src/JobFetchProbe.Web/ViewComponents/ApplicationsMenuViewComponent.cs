using JobFetchProbe.Models.Applications;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.ViewComponents;

/// <summary>
/// The Applications entry of the menu with its two numbers: open applications, and (red) the flagged ones. Without the
/// API the entry shows without numbers; the page itself then says what is wrong.
/// </summary>
public sealed class ApplicationsMenuViewComponent(IApplicationClient applications) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(bool active)
    {
        ApplicationsSummary? summary = null;
        try
        {
            summary = await applications.GetSummaryAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException || ex is HttpRequestException)
        {
            // No numbers.
        }
        return View(new ApplicationsMenuView(active, summary));
    }
}
