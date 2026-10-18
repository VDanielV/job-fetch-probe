using JobFetchProbe.Models.Acquisition;
using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.FetchJobListing;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Acquisition;

namespace JobFetchProbe.Console;

/// <summary>
/// The whole run on the console: starts the acquisition (step 1, then step 2, in Services), prints each step as it
/// goes, then one summary with the totals. Ctrl+C during step 2 stops after the current job; the next run continues
/// from there.
/// </summary>
public sealed class PipelineConsole(
    IAcquisitionService acquisition,
    FetchJobListingConsole jobListing,
    FetchJobDetailsConsole jobDetails,
    ConsolePaths paths) : IAcquisitionObserver
{
    private readonly CancellationTokenSource cts = new();

    /// <summary>Exit code when another acquisition (the API's) is running.</summary>
    public const int AlreadyRunning = 4;

    /// <returns>
    /// 0 when nothing is left to do (no page kept in the input folder, every job read or gone); 1 otherwise;
    /// <see cref="AlreadyRunning"/> when another acquisition is running.
    /// </returns>
    public async Task<int> RunAsync()
    {
        // Two acquisitions at once would double the requests to the site.
        if (await acquisition.IsRunningAsync())
        {
            ConsoleLog.Warn("An acquisition is already running (started from the API). Nothing was done; try again when it has finished.");
            ConsoleLog.Info("A run that ended without finishing counts as running until it has shown no sign of life for 2 minutes.");
            return AlreadyRunning;
        }

        try
        {
            var result = await acquisition.RunAsync(JournalStarter.Console, this, cts.Token);
            PrintSummary(result);
            if (result.NothingLeft)
            {
                return 0;
            }
            return 1;
        }
        finally
        {
            System.Console.CancelKeyPress -= OnCancel;
        }
    }

    public void JobListingStarting()
    {
        jobListing.Starting();
    }

    public void JobDetailsStarting()
    {
        jobDetails.Starting();
        // Only step 2 can be stopped this way; it takes long, and stops cleanly after the current job.
        System.Console.CancelKeyPress += OnCancel;
    }

    public void JournalRunStarted(long runId)
    {
        ConsoleLog.Info(FetchJobListingConsole.JournalLine(runId));
    }

    public void JobListingProgress(JobListingFetchProgress progress)
    {
        jobListing.Progress(progress);
    }

    public void JobListingFinished(JobListingFetchResult result)
    {
        jobListing.Finished(result);
    }

    public void JobDetailsEvent(JobDetailsFetchEvent e)
    {
        jobDetails.Print(e);
    }

    public void JobDetailsFinished(JobDetailsFetchResult result)
    {
    }

    private void OnCancel(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // keep the process alive so the summary is printed
        if (!cts.IsCancellationRequested)
        {
            ConsoleLog.Warn("Ctrl+C: stopping after the current job, everything read so far is kept…");
            cts.Cancel();
        }
    }

    private void PrintSummary(AcquisitionResult result)
    {
        var step1 = result.JobListing;
        var step2 = result.JobDetails;
        ConsoleLog.Heading("Summary");

        if (step1.Files.Count == 0)
        {
            Line("Input pages", "none");
        }
        else
        {
            var archived = step1.Files.Count(f => f.ArchivedTo is not null);
            var kept = step1.Files.Count - archived;
            Line("Input pages", $"{step1.Files.Count} found, {archived} read and archived, {kept} kept in the input folder");
            if (step1.ListingId is not null && step1.Batch is not null)
            {
                Line("Job links", $"{step1.Batch.Count} stored as listing {step1.ListingId}");
            }
            else
            {
                Line("Job links", "none stored (no page could be read)");
            }
        }

        Line("Descriptions", $"{step2.Fetched} read in this run, {step2.Failed} failed, {step2.Pending} not tried");
        Line(string.Empty, $"{step2.TotalRead} stored in total; {step2.StillUnread} still to read (tried again on the next run)");
        Line("Requests", $"{step2.Requests} sent in {ConsoleLog.Duration(step2.Elapsed)}, circuit breaker opened {step2.CircuitOpenings} time(s)");
        Line("Database", paths.Database);

        if (step2.StoppedReason is not null)
        {
            Line("Stopped early", step2.StoppedReason);
        }
        else if (result.NothingLeft)
        {
            Line("Result", "complete: every job has its description");
        }
    }

    private static void Line(string label, string text)
    {
        System.Console.WriteLine($"{label,-15}  {text}");
    }
}
