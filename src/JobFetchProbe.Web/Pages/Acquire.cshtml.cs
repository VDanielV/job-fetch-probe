using JobFetchProbe.Models.Acquisition;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// Acquire: explains the acquisition and starts it (POST api/JobFetcher). While one runs, or after starting one here
/// (?since=…), the explanation gives way to a progress pane read from the journal (api/Journal), and the page reloads
/// itself every <see cref="RefreshSeconds"/> seconds until the acquisition ends.
/// </summary>
public sealed class AcquireModel(IJobFetcherClient fetcher, IJournalClient journal, TimeProvider time) : PageModel
{
    public const int RefreshSeconds = 15;

    /// <summary>Null when the API is not reachable; <see cref="Unavailable"/> then says why.</summary>
    public AcquisitionView? View { get; private set; }

    public string? Unavailable { get; private set; }

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

    public async Task OnGetAsync([FromQuery] long? since = null, CancellationToken ct = default)
    {
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
