using JobFetchProbe.Models.Acquisition;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// Acquire: explains the acquisition, starts it (POST api/JobFetcher) and stops it (POST api/JobFetcher/stop). While one
/// runs, or after starting one here (?since=…), the explanation gives way to a pane with three tabs read from the
/// journal (api/Journal): Progress, Whole run and Summary (?tab=). The page reloads itself every
/// <see cref="RefreshSeconds"/> seconds until the acquisition ends. Errors show in a red pane at the end.
/// </summary>
public sealed class AcquireModel(IJobFetcherClient fetcher, IJournalClient journal, TimeProvider time) : PageModel
{
    public const int RefreshSeconds = 15;

    /// <summary>Null when the API is not reachable; <see cref="Unavailable"/> then says why.</summary>
    public AcquisitionView? View { get; private set; }

    public string? Unavailable { get; private set; }

    /// <summary>An error to show in the red pane: from the API, or from the acquisition itself.</summary>
    public PageError? Error
    {
        get
        {
            if (apiError is not null)
            {
                return apiError;
            }
            if (Unavailable is not null)
            {
                return PageError.Unreachable(Unavailable);
            }
            return View?.Error;
        }
    }

    /// <summary>The tab shown in the pane: progress, run or summary.</summary>
    public string Tab { get; private set; } = TabProgress;

    public const string TabProgress = "progress";
    public const string TabRun = "run";
    public const string TabSummary = "summary";

    /// <summary>When the button was pushed (?since=), kept in the tab links and after a stop.</summary>
    public long? Since { get; private set; }

    /// <summary>A stop was asked from this page: the Stop button shows "Stopping…" until the run ends.</summary>
    public bool StopRequested { get; private set; }

    /// <summary>The Stop button shows: the acquisition is running (not merely starting) and was started from the web.</summary>
    public bool CanStop
    {
        get
        {
            return View is not null && View.IsRunning && !View.IsStarting && !View.StartedFromConsole;
        }
    }

    private PageError? apiError;

    /// <summary>The answer to the button, shown once after the page reloads.</summary>
    public PageBanner? Banner { get; private set; }

    [TempData]
    public string? BannerKind { get; set; }

    [TempData]
    public string? BannerTitle { get; set; }

    [TempData]
    public string? BannerText { get; set; }

    /// <summary>The page reloads itself: an acquisition is starting or running.</summary>
    public bool Refresh
    {
        get
        {
            return View is not null && (View.IsRunning || View.IsStarting);
        }
    }

    /// <summary>The progress pane replaces the explanation pane.</summary>
    public bool ShowProgress
    {
        get
        {
            return View is not null && View.ShowProgress;
        }
    }

    public async Task OnGetAsync(
        [FromQuery] long? since = null,
        [FromQuery] string? tab = null,
        [FromQuery] bool stopping = false,
        CancellationToken ct = default)
    {
        Since = since;
        StopRequested = stopping;
        if (BannerKind is not null && BannerTitle is not null)
        {
            Banner = new PageBanner(BannerKind, BannerTitle, BannerText ?? string.Empty);
        }

        DateTimeOffset? pushed = null;
        if (since is { } seconds)
        {
            pushed = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        try
        {
            var runs = await journal.GetRecentRunsAsync(6, ct);
            var listingRun = runs.FirstOrDefault(r => r.Step == JournalStep.JobListing);
            var detailsRun = runs.FirstOrDefault(r => r.Step == JournalStep.JobDetails);

            JobListingJournalRun? listing = null;
            if (listingRun is not null)
            {
                listing = await journal.GetJobListingRunAsync(listingRun.RunId, ct);
            }
            JobDetailsJournalRun? details = null;
            if (detailsRun is not null)
            {
                details = await journal.GetJobDetailsRunAsync(detailsRun.RunId, ct);
            }

            View = AcquisitionView.Create(listing, details, pushed, time.GetLocalNow());
        }
        catch (ApiUnavailableException ex)
        {
            Unavailable = ex.Message;
        }
        catch (ApiErrorException ex)
        {
            apiError = PageError.From(ex.ErrorType, ex.Message);
        }

        Tab = ChooseTab(tab);
    }

    /// <summary>The Stop button. As with the start, the answer is shown after a reload.</summary>
    public async Task<IActionResult> OnPostStopAsync([FromForm] long? since = null, CancellationToken ct = default)
    {
        AcquisitionStop stop;
        try
        {
            stop = await fetcher.StopAsync(ct);
        }
        catch (ApiUnavailableException ex)
        {
            SetBanner("error", "The acquisition was not stopped.", ex.Message);
            return RedirectToPage(new { since });
        }
        catch (ApiErrorException ex)
        {
            SetBanner("error", "The acquisition was not stopped.", ex.Message);
            return RedirectToPage(new { since });
        }

        if (stop.State == AcquisitionStopState.NotRunning)
        {
            SetBanner("busy", "Nothing was stopped.", stop.Message);
            return RedirectToPage(new { since });
        }

        SetBanner("busy", $"Stop requested at {stop.At.ToLocalTime():HH:mm:ss}.", stop.Message + " What was read so far is kept.");
        return RedirectToPage(new { since, stopping = true });
    }

    /// <summary>Progress while the acquisition starts or runs, Summary once it has ended; the ?tab= choice wins.</summary>
    private string ChooseTab(string? asked)
    {
        if (asked is TabProgress or TabRun or TabSummary)
        {
            return asked;
        }
        if (View is not null && !View.IsRunning && !View.IsStarting && View.State is not null)
        {
            return TabSummary;
        }
        return TabProgress;
    }

    /// <summary>The button. The answer is shown after a reload (post, redirect, get), so a refresh never starts a second run.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        AcquisitionStart start;
        try
        {
            start = await fetcher.StartAsync(ct);
        }
        catch (ApiUnavailableException ex)
        {
            SetBanner("error", "The acquisition was not started.", ex.Message);
            return RedirectToPage();
        }
        catch (ApiErrorException ex)
        {
            SetBanner("error", "The acquisition was not started.", ex.Message);
            return RedirectToPage();
        }

        if (start.State == AcquisitionStartState.AlreadyRunning)
        {
            SetBanner("busy", "Not started: an acquisition is already running.", start.Message + " A second run would double the requests to LinkedIn.");
            return RedirectToPage();
        }

        SetBanner("ok", $"Acquisition started at {start.At.ToLocalTime():HH:mm}.",
            "It runs in the background on the API; you can leave this page. New jobs appear in Overview as they are read.");
        return RedirectToPage(new { since = start.At.ToUnixTimeSeconds() });
    }

    private void SetBanner(string kind, string title, string text)
    {
        BannerKind = kind;
        BannerTitle = title;
        BannerText = text;
    }
}
