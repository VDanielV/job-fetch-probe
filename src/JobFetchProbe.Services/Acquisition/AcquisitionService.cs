using JobFetchProbe.Models.Acquisition;
using JobFetchProbe.Models.FetchJobDetails;
using JobFetchProbe.Models.FetchJobListing;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.FetchJobDetails;
using JobFetchProbe.Services.FetchJobListing;
using JobFetchProbe.Services.Journal;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Services.Acquisition;

/// <summary>
/// Runs step 1, then step 2, each in its own scope (the fetchers are transient and use the typed HttpClient), and
/// records each step in the journal on the side: the steps themselves do not know about the journal.
/// </summary>
public sealed class AcquisitionService(IServiceScopeFactory scopes, IJournalService journal) : IAcquisitionService
{
    public Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        return journal.IsAnyRunActiveAsync(ct);
    }

    public async Task<AcquisitionResult> RunAsync(JournalStarter startedBy, IAcquisitionObserver observer, CancellationToken ct = default)
    {
        var jobListing = await FetchJobListingAsync(startedBy, observer, ct);
        var jobListingComplete = !jobListing.Stopped && jobListing.Files.All(f => f.Success && f.ArchivedTo is not null);

        // Stopped during step 1 (or right after it): step 2 does not start.
        if (jobListing.Stopped || ct.IsCancellationRequested)
        {
            return new AcquisitionResult(jobListing, null, jobListingComplete, false);
        }

        var jobDetails = await FetchJobDetailsAsync(startedBy, observer, ct);
        return new AcquisitionResult(jobListing, jobDetails, jobListingComplete, jobListingComplete && jobDetails.StillUnread == 0);
    }

    private async Task<JobListingFetchResult> FetchJobListingAsync(JournalStarter startedBy, IAcquisitionObserver observer, CancellationToken ct)
    {
        observer.JobListingStarting();
        await using var journalRun = await journal.StartJobListingRunAsync(startedBy, ct);
        observer.JournalRunStarted(journalRun.RunId);

        var progress = new CallbackProgress<JobListingFetchProgress>(p =>
        {
            observer.JobListingProgress(p);
            journalRun.Report(p);
        });

        JobListingFetchResult result;
        using (var scope = scopes.CreateScope())
        {
            try
            {
                result = await scope.ServiceProvider.GetRequiredService<IJobListingFetcher>().FetchAsync(progress, ct);
            }
            catch (Exception ex)
            {
                await journalRun.FailAsync(ex);
                throw;
            }
        }
        await journalRun.CompleteAsync(result);
        observer.JobListingFinished(result);
        return result;
    }

    private async Task<JobDetailsFetchResult> FetchJobDetailsAsync(JournalStarter startedBy, IAcquisitionObserver observer, CancellationToken ct)
    {
        observer.JobDetailsStarting();
        await using var journalRun = await journal.StartJobDetailsRunAsync(startedBy, ct);
        observer.JournalRunStarted(journalRun.RunId);

        var progress = new CallbackProgress<JobDetailsFetchEvent>(e =>
        {
            observer.JobDetailsEvent(e);
            journalRun.Report(e);
        });

        JobDetailsFetchResult result;
        using (var scope = scopes.CreateScope())
        {
            try
            {
                result = await scope.ServiceProvider.GetRequiredService<IJobDetailsFetcher>().FetchAsync(progress, ct);
            }
            catch (Exception ex)
            {
                await journalRun.FailAsync(ex);
                throw;
            }
        }
        await journalRun.CompleteAsync(result);
        observer.JobDetailsFinished(result);
        return result;
    }
}
