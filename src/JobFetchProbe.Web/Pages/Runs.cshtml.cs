using JobFetchProbe.Models.RunHistory;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// Runs: the past acquisitions, newest first, 25 per page (GET api/Runs). Acquisitions that did nothing are hidden
/// unless ?empty=1. Each line opens the run's page (/runs/{id}).
/// </summary>
public sealed class RunsModel(IRunsClient runs) : PageModel
{
    public const int PageSize = 25;

    /// <summary>Null when the API cannot answer, or there is no database yet; <see cref="Unavailable"/> then says why.</summary>
    public RunHistoryPage? List { get; private set; }

    public string? Unavailable { get; private set; }

    public PageError? Error { get; private set; }

    public bool ShowEmpty { get; private set; }

    public async Task OnGetAsync([FromQuery] int p = 1, [FromQuery] int empty = 0, CancellationToken ct = default)
    {
        ShowEmpty = empty == 1;
        try
        {
            List = await runs.GetPageAsync(Math.Max(1, p), PageSize, ShowEmpty, ct);
            if (List is null)
            {
                Unavailable = "There is no database yet: start the API, then run an acquisition.";
            }
        }
        catch (ApiUnavailableException ex)
        {
            Unavailable = ex.Message;
        }
        catch (ApiErrorException ex)
        {
            Error = PageError.From(ex.ErrorType, ex.Message);
        }
    }

    public int TotalPages
    {
        get
        {
            if (List is null || List.Total == 0)
            {
                return 1;
            }
            return (List.Total + PageSize - 1) / PageSize;
        }
    }

    /// <summary>This page's address with another page number or the empty-runs switch.</summary>
    public string Link(int page, bool? showEmpty = null)
    {
        var empty = showEmpty ?? ShowEmpty;
        var query = new List<string>();
        if (page > 1)
        {
            query.Add($"p={page}");
        }
        if (empty)
        {
            query.Add("empty=1");
        }
        if (query.Count == 0)
        {
            return "/runs";
        }
        return "/runs?" + string.Join("&", query);
    }

    /// <summary>A run's page: /runs/{id}, or /runs/{listing} for a listing without a journal.</summary>
    public static string RunLink(RunHistoryLine run)
    {
        if (run.RunId is { } id)
        {
            return $"/runs/{id}";
        }
        return $"/runs/{run.ListingId}";
    }
}
