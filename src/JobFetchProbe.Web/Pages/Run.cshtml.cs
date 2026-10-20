using JobFetchProbe.Models.Journal;
using JobFetchProbe.Models.RunHistory;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// One past acquisition (/runs/{id}, the step 1 run's journal ID): Summary and Whole run as the Acquire page shows them
/// at the end of a run (GET api/Journal/…), and Job links (GET api/Runs/{id}/job-links). A listing stored before the
/// journal existed (/runs/{listingId}) has the Job links tab only. Read only.
/// </summary>
public sealed class RunModel(IRunsClient runs, IJournalClient journal, TimeProvider time) : PageModel
{
    public const string TabSummary = "summary";
    public const string TabRun = "run";
    public const string TabLinks = "links";

    public const string ShowAll = "all";
    public const string ShowNew = "new";
    public const string ShowKnown = "known";

    /// <summary>The address's ID: a journal run ID, or a listing ID.</summary>
    public string Id { get; private set; } = string.Empty;

    /// <summary>The acquisition; null when unknown or when the API cannot answer.</summary>
    public RunHistoryLine? Run { get; private set; }

    /// <summary>Its journal as the Acquire page shows it; null for a listing without a journal.</summary>
    public AcquisitionView? View { get; private set; }

    /// <summary>The job links, loaded on the Job links tab only.</summary>
    public RunJobLinks? Links { get; private set; }

    public string Tab { get; private set; } = TabSummary;

    /// <summary>The Job links chip: all, new or known.</summary>
    public string Show { get; private set; } = ShowAll;

    public string? Unavailable { get; private set; }

    public PageError? Error { get; private set; }

    public bool HasJournal
    {
        get
        {
            return Run?.RunId is not null;
        }
    }

    public async Task OnGetAsync(string id, [FromQuery] string? tab = null, [FromQuery] string? show = null, CancellationToken ct = default)
    {
        Id = id;
        if (show is ShowNew or ShowKnown)
        {
            Show = show;
        }
        try
        {
            if (long.TryParse(id, out var runId))
            {
                await LoadRunAsync(runId, tab, ct);
            }
            else
            {
                Links = await runs.GetListingJobLinksAsync(id, ct);
                Run = Links?.Run;
                Tab = TabLinks;
            }
            if (Run is null)
            {
                Unavailable = $"There is no run {id}. It may be older than the journal keeps, or the address is wrong.";
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

    private async Task LoadRunAsync(long runId, string? tab, CancellationToken ct)
    {
        Run = await runs.GetRunAsync(runId, ct);
        if (Run is null)
        {
            return;
        }
        if (tab is TabRun or TabLinks)
        {
            Tab = tab;
        }

        var listing = await journal.GetJobListingRunAsync(runId, ct);
        JobDetailsJournalRun? details = null;
        if (Run.DetailsRunId is { } detailsId)
        {
            details = await journal.GetJobDetailsRunAsync(detailsId, ct);
        }
        View = AcquisitionView.Create(listing, details, null, time.GetLocalNow());
        View.UseListingCounts(Run.New, Run.Known);

        if (Tab == TabLinks)
        {
            Links = await runs.GetJobLinksAsync(runId, ct);
        }
    }

    /// <summary>The job links the chip lets through.</summary>
    public IReadOnlyList<RunJobLink> ShownLinks
    {
        get
        {
            if (Links is null)
            {
                return [];
            }
            if (Show == ShowNew)
            {
                return Links.Links.Where(l => l.IsNew).ToList();
            }
            if (Show == ShowKnown)
            {
                return Links.Links.Where(l => !l.IsNew).ToList();
            }
            return Links.Links;
        }
    }

    /// <summary>This run's address with a tab and a chip.</summary>
    public string Link(string tab, string? show = null)
    {
        var query = new List<string>();
        if (tab != TabSummary || !HasJournal)
        {
            query.Add($"tab={tab}");
        }
        if (show is ShowNew or ShowKnown)
        {
            query.Add($"show={show}");
        }
        var path = $"/runs/{Uri.EscapeDataString(Id)}";
        if (query.Count == 0)
        {
            return path;
        }
        return path + "?" + string.Join("&", query);
    }

    /// <summary>Where a job opens: Applications when applied to, otherwise the Overview (with the archived jobs shown when it is archived).</summary>
    public static string JobLink(RunJobLink link)
    {
        if (link.AppliedAt is not null)
        {
            return $"/applications?job={link.JobId}";
        }
        if (link.ArchivedAt is not null)
        {
            return $"/?archived=1&job={link.JobId}";
        }
        return $"/?job={link.JobId}";
    }
}
