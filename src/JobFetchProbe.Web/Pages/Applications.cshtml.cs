using JobFetchProbe.Models.Applications;
using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Models.Fit;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// Applications: the jobs the owner applied to, flagged ones first, 25 per page (?p=2), with the selected one in the pane
/// (?job=…). Closed ones are listed with ?closed=1; chips narrow to one stage (?stage=Interview) or the flagged ones
/// (?flagged=1); ?sort=fit lists the best fit first. From the pane: change the stage, add a note, check the fit again, or
/// undo "I applied". Everything goes through
/// GET/PUT/POST/DELETE api/Applications; the job's description comes from GET api/JobCatalog/{jobId}.
/// </summary>
public sealed class ApplicationsModel(IApplicationClient applications, IJobCatalogClient catalog, IJobFitClient fits) : PageModel
{
    public const int PageSize = 25;

    /// <summary>The closed applications are listed (?closed=1) instead of the open ones.</summary>
    public bool ShowClosed { get; private set; }

    /// <summary>Only this stage (?stage=), or null.</summary>
    public ApplicationStage? Stage { get; private set; }

    /// <summary>Flagged first (the default), or the best latest fit first (?sort=fit).</summary>
    public ApplicationSort Sort { get; private set; }

    /// <summary>The fit of the application in the pane: its latest stored score and the earlier ones; null when never scored.</summary>
    public FitView? Fit { get; private set; }

    /// <summary>Only the flagged applications (?flagged=1).</summary>
    public bool OnlyFlagged { get; private set; }

    /// <summary>Null when the list cannot be shown; <see cref="Unavailable"/> then says why.</summary>
    public ApplicationPage? List { get; private set; }

    public string? Unavailable { get; private set; }

    public string? SelectedId { get; private set; }

    /// <summary>The application in the pane, with its timeline; null when none is selected or it is not an application.</summary>
    public Application? Selected { get; private set; }

    /// <summary>The job of the application in the pane (link, facts, description); null if it could not be read.</summary>
    public StoredJob? Job { get; private set; }

    public PageBanner? Banner { get; private set; }

    [TempData]
    public string? BannerKind { get; set; }

    [TempData]
    public string? BannerTitle { get; set; }

    [TempData]
    public string? BannerText { get; set; }

    /// <summary>After closing an application: its ID and the stage it had, for the Undo next to the green line.</summary>
    [TempData]
    public string? ClosedJobId { get; set; }

    [TempData]
    public string? ClosedFrom { get; set; }

    /// <summary>An undo was refused because the owner wrote notes: the job and how many, for the warning in the pane.</summary>
    [TempData]
    public string? UndoWarningJobId { get; set; }

    [TempData]
    public int UndoWarningNotes { get; set; }

    /// <summary>A note that could not be added, put back in the box.</summary>
    [TempData]
    public string? NoteDraft { get; set; }

    /// <summary>The application in the pane asked for confirmation before an undo that deletes its notes.</summary>
    public int UndoWarning { get; private set; }

    /// <summary>The undo of the last closing (the application and its previous stage), or null.</summary>
    public (string JobId, ApplicationStage Previous)? UndoClose { get; private set; }

    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int page = 1, [FromQuery(Name = "job")] string? job = null, [FromQuery(Name = "closed")] int closed = 0,
        [FromQuery(Name = "stage")] string? stage = null, [FromQuery(Name = "flagged")] int flagged = 0,
        [FromQuery(Name = "sort")] string? sort = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(job))
        {
            SelectedId = job.Trim();
        }
        SetState(closed, stage, flagged, sort);
        if (BannerKind is not null && BannerTitle is not null)
        {
            Banner = new PageBanner(BannerKind, BannerTitle, BannerText ?? string.Empty);
        }
        if (ClosedJobId is not null && Enum.TryParse<ApplicationStage>(ClosedFrom, out var previous))
        {
            UndoClose = (ClosedJobId, previous);
        }
        if (UndoWarningJobId is not null && UndoWarningJobId == SelectedId)
        {
            UndoWarning = UndoWarningNotes;
        }

        try
        {
            List = await applications.GetPageAsync(ShowClosed, Stage, OnlyFlagged, Math.Max(1, page), PageSize, Sort, ct);
            if (SelectedId is not null)
            {
                Selected = await applications.GetAsync(SelectedId, ct);
                if (Selected is null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                }
                else
                {
                    Job = await catalog.GetJobAsync(SelectedId, ct);
                    Fit = await StoredFitAsync(SelectedId, null, ct);
                }
            }
        }
        catch (ApiUnavailableException ex)
        {
            Unavailable = ex.Message;
        }
        catch (ApiErrorException ex)
        {
            Unavailable = "The API could not read the applications: " + ex.Message;
        }
    }

    /// <summary>
    /// A new stage for the application in the pane. If that moves it out of this list (closing it in the open list, or
    /// reopening it in the closed list), the next one opens and a green line offers Undo.
    /// </summary>
    public async Task<IActionResult> OnPostStageAsync(
        string jobId, ApplicationStage stage, int index, int p = 1, int closed = 0, string? filter = null, int flagged = 0,
        string? sort = null, CancellationToken ct = default)
    {
        SetState(closed, filter, flagged, sort);
        try
        {
            var before = await applications.GetAsync(jobId, ct);
            var after = await applications.SetStageAsync(jobId, stage, ct);
            var left = ApplicationStageText.IsClosed(after.Summary.Stage) != ShowClosed;
            if (before is null || before.Summary.Stage == stage || !left)
            {
                return Back(p, jobId);
            }

            var title = after.Summary.Title ?? jobId;
            if (ApplicationStageText.IsClosed(stage))
            {
                SetBanner("ok", $"Closed as {ApplicationStageText.Label(stage)}: ", title + ".");
            }
            else
            {
                SetBanner("ok", $"Reopened as {ApplicationStageText.Label(stage)}: ", title + ".");
            }
            ClosedJobId = jobId;
            ClosedFrom = before.Summary.Stage.ToString();
            var (page, next) = await NextAsync(p, index, jobId, ct);
            return Back(page, next);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            SetBanner("error", "The stage was not changed.", " " + ex.Message);
            return Back(p, jobId);
        }
    }

    /// <summary>Undo of the last closing: puts the previous stage back and opens the application again.</summary>
    public async Task<IActionResult> OnPostRestoreAsync(
        string jobId, ApplicationStage previous, int p = 1, int closed = 0, string? filter = null, int flagged = 0, string? sort = null, CancellationToken ct = default)
    {
        SetState(closed, filter, flagged, sort);
        try
        {
            await applications.SetStageAsync(jobId, previous, ct);
            return Back(p, jobId);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            SetBanner("error", "Nothing was undone.", " " + ex.Message);
            return Back(p, null);
        }
    }

    /// <summary>Adds the note typed in the pane, dated now.</summary>
    public async Task<IActionResult> OnPostNoteAsync(
        string jobId, string? text, int p = 1, int closed = 0, string? filter = null, int flagged = 0, string? sort = null, CancellationToken ct = default)
    {
        SetState(closed, filter, flagged, sort);
        try
        {
            await applications.AddNoteAsync(jobId, text ?? string.Empty, ct);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            SetBanner("error", "The note was not added.", " " + ex.Message);
            NoteDraft = text;
        }
        return Back(p, jobId);
    }

    /// <summary>
    /// Undo of a mistaken "I applied": the job goes back to the Overview. When the owner wrote notes, the pane first asks
    /// (withNotes=1 confirms, and the notes are deleted with the application).
    /// </summary>
    public async Task<IActionResult> OnPostUndoAsync(
        string jobId, int index, int withNotes = 0, int p = 1, int closed = 0, string? filter = null, int flagged = 0,
        string? sort = null, CancellationToken ct = default)
    {
        SetState(closed, filter, flagged, sort);
        try
        {
            var title = (await applications.GetAsync(jobId, ct))?.Summary.Title ?? jobId;
            var notes = await applications.UndoAsync(jobId, withNotes == 1, ct);
            if (notes > 0)
            {
                UndoWarningJobId = jobId;
                UndoWarningNotes = notes;
                return Back(p, jobId);
            }
            SetBanner("ok", "Back in the Overview: ", title + ". It is no longer in your applications.");
            var (page, next) = await NextAsync(p, index, jobId, ct);
            return Back(page, next);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            SetBanner("error", "Nothing was undone.", " " + ex.Message);
            return Back(p, jobId);
        }
    }

    /// <summary>
    /// "Check fit" in the pane (POST api/JobFit, which keeps the score). The script asks for the card only
    /// (X-Requested-With: fetch); without it the page comes back and shows the stored score.
    /// </summary>
    public async Task<IActionResult> OnPostFitAsync(
        string jobId, int p = 1, int closed = 0, string? filter = null, int flagged = 0, string? sort = null, CancellationToken ct = default)
    {
        SetState(closed, filter, flagged, sort);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        FitView view;
        try
        {
            var result = await fits.AssessAsync(jobId, ct);
            view = new FitView(jobId, result, null, (int)Math.Round(clock.Elapsed.TotalSeconds), ArchiveHint: false);
            view = await StoredFitAsync(jobId, view, ct) ?? view;
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            view = new FitView(jobId, null, ex.Message, (int)Math.Round(clock.Elapsed.TotalSeconds), ArchiveHint: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult(); // the browser stopped waiting
        }

        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return Partial("_FitResult", view);
        }
        if (view.Error is not null)
        {
            SetBanner("error", "Couldn't score this job.", " " + view.Error);
        }
        return Back(p, jobId);
    }

    /// <summary>The fit card: <paramref name="fresh"/> with the earlier stored scores, or the latest stored score, or null.</summary>
    private async Task<FitView?> StoredFitAsync(string jobId, FitView? fresh, CancellationToken ct)
    {
        JobFitHistory? history = null;
        try
        {
            history = await fits.GetHistoryAsync(jobId, ct);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            // The pane works without the history.
        }
        if (history is null)
        {
            return fresh;
        }
        if (fresh is not null)
        {
            return fresh.WithHistory(history);
        }
        var stored = FitView.FromHistory(jobId, history);
        if (stored is null)
        {
            return null;
        }
        return stored with { ArchiveHint = false };
    }

    /// <summary>The application now at the position the one that left had (the next one), else the previous one.</summary>
    private async Task<(int Page, string? Next)> NextAsync(int p, int index, string goneId, CancellationToken ct)
    {
        try
        {
            var refreshed = await applications.GetPageAsync(ShowClosed, Stage, OnlyFlagged, Math.Max(1, p), PageSize, Sort, ct);
            var items = refreshed.Applications.Where(a => a.JobId != goneId).ToList();
            if (items.Count == 0)
            {
                return (refreshed.Page, null);
            }
            var at = Math.Clamp(index, 0, items.Count - 1);
            return (refreshed.Page, items[at].JobId);
        }
        catch (Exception ex) when (ex is ApiUnavailableException || ex is ApiErrorException)
        {
            return (p, null);
        }
    }

    /// <summary>The page numbers to show: the first, the last, and the current one with its neighbours; null marks a gap.</summary>
    public IReadOnlyList<int?> PageNumbers()
    {
        var result = new List<int?>();
        if (List is null)
        {
            return result;
        }
        int? previous = null;
        for (var n = 1; n <= List.TotalPages; n++)
        {
            if (n == 1 || n == List.TotalPages || Math.Abs(n - List.Page) <= 1)
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

    /// <summary>Where an application sits on this page (0-based), or -1.</summary>
    public int PositionOf(string jobId)
    {
        if (List is null)
        {
            return -1;
        }
        for (var i = 0; i < List.Applications.Count; i++)
        {
            if (List.Applications[i].JobId == jobId)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The application before or after the one in the pane, on this page, for the ↑ / ↓ keys; null at either end.</summary>
    public string? Neighbour(int step)
    {
        if (List is null || SelectedId is null)
        {
            return null;
        }
        var index = PositionOf(SelectedId);
        if (index < 0)
        {
            return null;
        }
        var other = index + step;
        if (other < 0 || other >= List.Applications.Count)
        {
            return null;
        }
        return Link(List.Page, List.Applications[other].JobId);
    }

    /// <summary>The count for one stage.</summary>
    public int CountOf(ApplicationStage stage)
    {
        if (List is null)
        {
            return 0;
        }
        var found = List.Stages.FirstOrDefault(s => s.Stage == stage);
        if (found is null)
        {
            return 0;
        }
        return found.Count;
    }

    /// <summary>
    /// The address of this page with the current list settings, on <paramref name="page"/> with <paramref name="job"/> open.
    /// The optional arguments change one setting; a chip (stage or flagged) replaces the other chip.
    /// </summary>
    public string Link(
        int page, string? job, bool? closed = null, ApplicationStage? stage = null, bool? flagged = null, bool allChips = false,
        ApplicationSort? sort = null)
    {
        var showClosed = closed ?? ShowClosed;
        var query = new QueryBuilder { { "p", Math.Max(1, page).ToString() } };
        if (job is not null)
        {
            query.Add("job", job);
        }
        if (showClosed)
        {
            query.Add("closed", "1");
        }
        var chipStage = Stage;
        var chipFlagged = OnlyFlagged;
        if (allChips || closed is not null)
        {
            chipStage = null;
            chipFlagged = false;
        }
        if (stage is not null)
        {
            chipStage = stage;
            chipFlagged = false;
        }
        if (flagged is not null)
        {
            chipFlagged = flagged.Value;
            chipStage = null;
        }
        if (chipStage is not null)
        {
            query.Add("stage", chipStage.Value.ToString());
        }
        if (chipFlagged)
        {
            query.Add("flagged", "1");
        }
        if ((sort ?? Sort) == ApplicationSort.Fit)
        {
            query.Add("sort", "fit");
        }
        return Url.Page("/Applications") + query.ToQueryString();
    }

    private void SetState(int closed, string? stage, int flagged, string? sort)
    {
        if (string.Equals(sort, "fit", StringComparison.OrdinalIgnoreCase))
        {
            Sort = ApplicationSort.Fit;
        }
        ShowClosed = closed == 1;
        if (Enum.TryParse<ApplicationStage>(stage, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            Stage = parsed;
        }
        OnlyFlagged = flagged == 1 && !ShowClosed;
    }

    private void SetBanner(string kind, string title, string text)
    {
        BannerKind = kind;
        BannerTitle = title;
        BannerText = text;
    }

    private RedirectResult Back(int page, string? job)
    {
        return Redirect(Link(page, job));
    }

    /// <summary>A stored time in this computer's time zone, e.g. "2026-10-15 22:30".</summary>
    public static string When(DateTimeOffset at)
    {
        return at.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}
