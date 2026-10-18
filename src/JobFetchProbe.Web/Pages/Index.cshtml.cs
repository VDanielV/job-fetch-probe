using JobFetchProbe.Models.Archive;
using JobFetchProbe.Models.Catalog;
using System.Diagnostics;
using System.Text.Json;
using JobFetchProbe.Models.Filter;
using JobFetchProbe.Models.Fit;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// Overview: the jobs to review, newest first, one page at a time (?p=2), and the selected job in the details pane (?job=…).
/// Both come from the API: GET api/JobCatalog?page=&amp;pageSize= and GET api/JobCatalog/{jobId}. Archived jobs are left
/// out unless ?archived=1. Jobs are archived from here: the ticked rows (ArchiveSelected), or the job in the pane
/// (ArchiveOpen, which then opens the next job); both go to POST api/ArchivedJobs and come back here.
/// A text filter (?q=…&amp;in=title) and the page size (?size=50, remembered in a cookie) narrow the list; every link,
/// form and redirect keeps them.
/// </summary>
public sealed class IndexModel(IJobCatalogClient api, IJobArchiveClient archive, IJobFitClient fits) : PageModel
{
    public const int DefaultPageSize = 25;
    public const string SizeCookie = "overview.size";

    /// <summary>The page sizes on offer.</summary>
    public static IReadOnlyList<int> PageSizes { get; } = [25, 50];

    /// <summary>The archived jobs are listed too (?archived=1).</summary>
    public bool ShowArchived { get; private set; }

    /// <summary>The text filter as typed (?q=), or null.</summary>
    public string? Query { get; private set; }

    /// <summary>Where the text filter looks (?in=title, else the title and the description).</summary>
    public FilterScope Scope { get; private set; }

    /// <summary>Jobs per page: 25 or 50.</summary>
    public int Size { get; private set; } = DefaultPageSize;

    /// <summary>The city chips that are on (?place=valencia&amp;place=spain).</summary>
    public IReadOnlyList<string> Places { get; private set; } = [];

    /// <summary>The work-mode chips that are on (?mode=hybrid).</summary>
    public IReadOnlyList<string> Modes { get; private set; } = [];

    /// <summary>A text filter is on.</summary>
    public bool Filtering
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Query);
        }
    }

    /// <summary>Any filter is on: text, city or work mode.</summary>
    public bool AnyFilter
    {
        get
        {
            return Filtering || Places.Count > 0 || Modes.Count > 0;
        }
    }

    /// <summary>The outcome of the last archive request, shown once after the redirect.</summary>
    public PageBanner? Banner { get; private set; }

    [TempData]
    public string? BannerKind { get; set; }

    [TempData]
    public string? BannerTitle { get; set; }

    [TempData]
    public string? BannerText { get; set; }

    /// <summary>Without the script: the score of the job in the pane, carried over the redirect once (JSON).</summary>
    [TempData]
    public string? FitJson { get; set; }

    /// <summary>The score of the job in the pane, when "Check fit" was used without the script; null otherwise.</summary>
    public FitView? Fit { get; private set; }

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
        [FromQuery(Name = "archived")] int archived = 0,
        [FromQuery(Name = "q")] string? q = null,
        [FromQuery(Name = "in")] string? scope = null,
        [FromQuery(Name = "size")] int? size = null,
        [FromQuery(Name = "place")] string[]? place = null,
        [FromQuery(Name = "mode")] string[]? mode = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(job))
        {
            SelectedId = job.Trim();
        }
        SetState(archived, q, scope, size, place, mode);
        if (size is not null && PageSizes.Contains(size.Value))
        {
            Response.Cookies.Append(SizeCookie, size.Value.ToString(), new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax });
        }
        if (BannerKind is not null && BannerTitle is not null)
        {
            Banner = new PageBanner(BannerKind, BannerTitle, BannerText ?? string.Empty);
        }
        if (FitJson is not null)
        {
            var fit = JsonSerializer.Deserialize<FitView>(FitJson, FitJsonOptions);
            if (fit is not null && fit.JobId == SelectedId)
            {
                Fit = fit;
            }
        }

        try
        {
            // The API rejects a page below 1; a page past the end it corrects itself (the result says which page).
            Jobs = await api.GetPageAsync(Math.Max(1, page), Size, ShowArchived, Query, Scope, Places, Modes, ct);
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

    /// <summary>A: archives the ticked rows with one reason, then shows the same page again (refilled), keeping the pane open.</summary>
    public async Task<IActionResult> OnPostArchiveSelectedAsync(
        string[]? jobIds, ArchiveReason reason, string? note, int p = 1, int archived = 0, string? job = null,
        string? q = null, [FromForm(Name = "in")] string? scope = null, int? size = null, string[]? place = null, string[]? mode = null,
        CancellationToken ct = default)
    {
        SetState(archived, q, scope, size, place, mode);
        var ids = jobIds ?? [];
        var result = await TryArchiveAsync(ids, reason, note, ct);
        if (result is not null && job is not null && ids.Contains(job))
        {
            job = null; // the job in the pane was archived: close the pane
        }
        return Back(p, job);
    }

    /// <summary>C: archives the job in the pane, then opens the job that came after it (it now sits at the same position).</summary>
    public async Task<IActionResult> OnPostArchiveOpenAsync(
        string jobId, ArchiveReason reason, string? note, int index, int p = 1, int archived = 0,
        string? q = null, [FromForm(Name = "in")] string? scope = null, int? size = null, string[]? place = null, string[]? mode = null,
        CancellationToken ct = default)
    {
        SetState(archived, q, scope, size, place, mode);
        var result = await TryArchiveAsync([jobId], reason, note, ct);
        if (result is null)
        {
            return Back(p, jobId);
        }

        try
        {
            // Under a text filter the list holds only the matches, so "the next job" is the next match.
            var refreshed = await api.GetPageAsync(Math.Max(1, p), Size, ShowArchived, Query, Scope, Places, Modes, ct);
            var next = FirstToReview(refreshed.Jobs, index, jobId);
            var page = refreshed.Page;
            if (next is null && refreshed.Page < refreshed.TotalPages)
            {
                var following = await api.GetPageAsync(refreshed.Page + 1, Size, ShowArchived, Query, Scope, Places, Modes, ct);
                next = FirstToReview(following.Jobs, 0, jobId);
                if (next is not null)
                {
                    page = following.Page;
                }
            }
            return Back(page, next);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            return Back(p, null); // archived; the next job could not be worked out, so the pane closes
        }
    }

    /// <summary>
    /// "Check fit": scores the job in the pane with the local model (POST api/JobFit; up to about two minutes). The page's
    /// script asks for the result card only (X-Requested-With: fetch) and puts it in the pane; without the script, the
    /// page comes back with the card. Leaving the page or Cancel stops the request, and with it the API's call to Ollama.
    /// </summary>
    public async Task<IActionResult> OnPostFitAsync(
        string jobId, int p = 1, int archived = 0, string? q = null, [FromForm(Name = "in")] string? scope = null, int? size = null,
        string[]? place = null, string[]? mode = null, CancellationToken ct = default)
    {
        SetState(archived, q, scope, size, place, mode);
        var clock = Stopwatch.StartNew();
        FitView view;
        try
        {
            var result = await fits.AssessAsync(jobId, ct);
            view = new FitView(jobId, result, null, (int)Math.Round(clock.Elapsed.TotalSeconds));
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            view = new FitView(jobId, null, ex.Message, (int)Math.Round(clock.Elapsed.TotalSeconds));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult(); // the browser stopped waiting (Cancel, or another job opened)
        }

        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return Partial("_FitResult", view);
        }
        FitJson = JsonSerializer.Serialize(view, FitJsonOptions);
        return Back(p, jobId);
    }

    private static readonly JsonSerializerOptions FitJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

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

    /// <summary>The job before or after the one in the pane, on this page, for the ↑ / ↓ keys; null at either end.</summary>
    public string? Neighbour(int step)
    {
        if (Jobs is null || SelectedId is null)
        {
            return null;
        }
        var index = PositionOf(SelectedId);
        if (index < 0)
        {
            return null;
        }
        var other = index + step;
        if (other < 0 || other >= Jobs.Jobs.Count)
        {
            return null;
        }
        return Link(Jobs.Page, Jobs.Jobs[other].JobId);
    }

    /// <summary>Where a job sits on this page (0-based), or -1.</summary>
    public int PositionOf(string jobId)
    {
        if (Jobs is null)
        {
            return -1;
        }
        for (var i = 0; i < Jobs.Jobs.Count; i++)
        {
            if (Jobs.Jobs[i].JobId == jobId)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Sends the archive request; the outcome (or the error) is shown by the next page.</summary>
    private async Task<ArchiveResult?> TryArchiveAsync(IReadOnlyList<string> jobIds, ArchiveReason reason, string? note, CancellationToken ct)
    {
        try
        {
            var result = await archive.ArchiveAsync(new ArchiveRequest(jobIds, reason, note), ct);
            SetBanner("ok", ArchivedTitle(result), Details(result, reason, note));
            return result;
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            SetBanner("error", "Nothing was archived.", " " + ex.Message);
            return null;
        }
    }

    private static string ArchivedTitle(ArchiveResult result)
    {
        if (result.Archived == 1)
        {
            return "1 job archived.";
        }
        return $"{result.Archived} jobs archived.";
    }

    private static string Details(ArchiveResult result, ArchiveReason reason, string? note)
    {
        var text = " " + ArchiveReasonText.Label(reason);
        if (!string.IsNullOrWhiteSpace(note))
        {
            text += ": " + note.Trim();
        }
        if (!text.EndsWith('.') && !text.EndsWith('!') && !text.EndsWith('?'))
        {
            text += ".";
        }
        if (result.AlreadyArchived > 0)
        {
            text += $" {result.AlreadyArchived} already archived (unchanged).";
        }
        if (result.NotFound > 0)
        {
            text += $" {result.NotFound} not in the database.";
        }
        return text;
    }

    private void SetBanner(string kind, string title, string text)
    {
        BannerKind = kind;
        BannerTitle = title;
        BannerText = text;
    }

    /// <summary>The first job to review at or after <paramref name="from"/>, other than the one just archived.</summary>
    private static string? FirstToReview(IReadOnlyList<JobSummary> jobs, int from, string archivedId)
    {
        for (var i = Math.Max(0, from); i < jobs.Count; i++)
        {
            if (jobs[i].Archive is null && jobs[i].JobId != archivedId)
            {
                return jobs[i].JobId;
            }
        }
        return null;
    }

    /// <summary>
    /// The address of this page with the current list settings (filter, scope, chips, size, archived), on
    /// <paramref name="page"/> with <paramref name="job"/> open. The optional arguments change one setting;
    /// <paramref name="clearFilter"/> drops the text, <paramref name="clearAll"/> the text and the chips.
    /// </summary>
    public string Link(
        int page, string? job, bool? showArchived = null, FilterScope? scope = null, int? size = null, bool clearFilter = false,
        IReadOnlyList<string>? places = null, IReadOnlyList<string>? modes = null, bool clearAll = false)
    {
        var archived = showArchived ?? ShowArchived;
        var inScope = scope ?? Scope;
        var pageSize = size ?? Size;
        var query = new QueryBuilder { { "p", Math.Max(1, page).ToString() } };
        if (job is not null)
        {
            query.Add("job", job);
        }
        if (archived)
        {
            query.Add("archived", "1");
        }
        if (!clearFilter && !clearAll && Filtering)
        {
            query.Add("q", Query!);
        }
        if (inScope == FilterScope.Title)
        {
            query.Add("in", "title");
        }
        if (!clearAll)
        {
            foreach (var place in places ?? Places)
            {
                query.Add("place", place);
            }
            foreach (var mode in modes ?? Modes)
            {
                query.Add("mode", mode);
            }
        }
        if (pageSize != DefaultPageSize)
        {
            query.Add("size", pageSize.ToString());
        }
        return Url.Page("/Index") + query.ToQueryString();
    }

    /// <summary>The address with one chip switched on or off (back to page 1, keeping the job open).</summary>
    public string ToggleChip(string row, string key)
    {
        if (row == "place")
        {
            return Link(1, SelectedId, places: Toggle(Places, key));
        }
        return Link(1, SelectedId, modes: Toggle(Modes, key));
    }

    private static List<string> Toggle(IReadOnlyList<string> keys, string key)
    {
        var result = keys.ToList();
        if (!result.Remove(key))
        {
            result.Add(key);
        }
        return result;
    }

    /// <summary>Reads the list settings from the address or a form; the page size falls back to the cookie, then 25.</summary>
    private void SetState(int archived, string? query, string? scope, int? size, string[]? places, string[]? modes)
    {
        ShowArchived = archived == 1;
        if (!string.IsNullOrWhiteSpace(query))
        {
            Query = query.Trim();
        }
        if (string.Equals(scope, "title", StringComparison.OrdinalIgnoreCase))
        {
            Scope = FilterScope.Title;
        }
        else
        {
            Scope = FilterScope.All;
        }
        Places = Clean(places);
        Modes = Clean(modes);

        if (size is not null && PageSizes.Contains(size.Value))
        {
            Size = size.Value;
        }
        else if (int.TryParse(Request.Cookies[SizeCookie], out var remembered) && PageSizes.Contains(remembered))
        {
            Size = remembered;
        }
    }

    private static List<string> Clean(string[]? keys)
    {
        return (keys ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private RedirectResult Back(int page, string? job)
    {
        return Redirect(Link(page, job));
    }

    /// <summary>A stored time in this computer's time zone, e.g. "2026-10-15 22:30".</summary>
    public static string When(DateTimeOffset at) => at.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
