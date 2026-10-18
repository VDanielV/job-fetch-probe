using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.FetchJobListing;
using JobFetchProbe.Models.Jobs;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Acquisition;

/// <summary>Writes an acquisition's progress to the log, one line per page, job and circuit change (the API's window).</summary>
public sealed class LoggingAcquisitionObserver(ILogger<LoggingAcquisitionObserver> logger) : IAcquisitionObserver
{
    public void JobListingStarting()
    {
        logger.LogInformation("Step 1: saved search pages → job links");
    }

    public void JobDetailsStarting()
    {
        logger.LogInformation("Step 2: job links → job descriptions");
    }

    public void JournalRunStarted(long runId)
    {
        if (runId > 0)
        {
            logger.LogInformation("Journal: run {RunId}", runId);
        }
        else
        {
            logger.LogWarning("Journal: not recorded this time; the run continues.");
        }
    }

    public void JobListingProgress(JobListingFetchProgress progress)
    {
        if (progress.Result is not { } result)
        {
            return;
        }
        if (result.Success)
        {
            logger.LogInformation("[{Index}/{Total}] {File}: {Jobs} jobs ({NewJobs} new)", progress.Index, progress.Total, progress.File, result.Jobs, result.NewJobs);
        }
        else
        {
            logger.LogWarning("[{Index}/{Total}] {File}: kept in the input folder. {Message}", progress.Index, progress.Total, progress.File, result.Message);
        }
    }

    public void JobListingFinished(JobListingFetchResult result)
    {
        if (result.Files.Count == 0)
        {
            logger.LogInformation("No saved pages in the input folder. Nothing new to add.");
        }
        else if (result.ListingId is null || result.Batch is null)
        {
            logger.LogWarning("No page could be read, so nothing was stored. The pages stay in the input folder.");
        }
        else
        {
            var archived = result.Files.Count(f => f.ArchivedTo is not null);
            logger.LogInformation("Stored {Count} unique job links as listing {ListingId}; {Archived} page(s) archived.", result.Batch.Count, result.ListingId, archived);
        }
    }

    public void JobDetailsEvent(JobDetailsFetchEvent e)
    {
        switch (e.Kind)
        {
            case JobDetailsFetchEventKind.Started:
                logger.LogInformation("{ToRead} job(s) without a description; one request every {Delay}.", e.ToRead, e.Wait);
                break;

            case JobDetailsFetchEventKind.Job:
                if (e.Status == JobResultStatus.Read)
                {
                    logger.LogInformation("[{Index}/{Total}] ok {JobId} {Title} ({ToRead} to go)", e.Index, e.Total, e.Job?.JobId, e.Title ?? e.Job?.Title, e.ToRead);
                }
                else if (e.Status == JobResultStatus.Blocked)
                {
                    logger.LogWarning("[{Index}/{Total}] REFUSED {JobId} ({ToRead} to go)", e.Index, e.Total, e.Job?.JobId, e.ToRead);
                }
                else if (e.Gone)
                {
                    logger.LogWarning("[{Index}/{Total}] gone {JobId}: the job page no longer exists", e.Index, e.Total, e.Job?.JobId);
                }
                else
                {
                    logger.LogWarning("[{Index}/{Total}] failed {JobId}: {Message}", e.Index, e.Total, e.Job?.JobId, e.Message);
                }
                break;

            case JobDetailsFetchEventKind.CircuitOpened:
                logger.LogWarning("Circuit OPEN ({Openings} of {StopAfter}): {Reason}. No requests for {Wait}.", e.Openings, e.StopAfterOpenings, e.Message, e.Wait);
                break;

            case JobDetailsFetchEventKind.Waiting:
                logger.LogInformation("Circuit open, trial request in {Wait}", e.Wait);
                break;

            case JobDetailsFetchEventKind.CircuitHalfOpen:
                logger.LogInformation("Circuit HALF-OPEN: sending one trial request.");
                break;

            case JobDetailsFetchEventKind.CircuitClosed:
                logger.LogInformation("Circuit CLOSED: continuing at the normal pace.");
                break;

            case JobDetailsFetchEventKind.Stopped:
                logger.LogWarning("Stopped: {Reason} ({ToRead} job(s) not tried in this run)", e.Message, e.ToRead);
                break;
        }
    }

    public void JobDetailsFinished(JobDetailsFetchResult result)
    {
        logger.LogInformation(
            "Descriptions: {Fetched} read, {Failed} failed, {Pending} not tried; {StillUnread} still to read, {TotalRead} stored in total; {Requests} request(s), circuit opened {Openings} time(s).",
            result.Fetched, result.Failed, result.Pending, result.StillUnread, result.TotalRead, result.Requests, result.CircuitOpenings);
    }
}
