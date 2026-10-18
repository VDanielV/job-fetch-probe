using JobFetchProbe.Models.FetchJobListing;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Abstractions.Journal;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Journal;

/// <summary>
/// Turns step 1 progress into journal writes: the run's state after each page, plus one entry per page. Without a
/// repository (the journal could not be started) it records nothing.
/// </summary>
internal sealed class JobListingRunRecorder : IJobListingRunRecorder
{
    private readonly RunRecorderCore<JobListingJournalRun, JobListingJournalEntry> core;

    public JobListingRunRecorder(IJournalRepository? repository, JobListingJournalRun state, JournalOptions options, TimeProvider time, ILogger logger)
    {
        core = new RunRecorderCore<JobListingJournalRun, JobListingJournalEntry>(
            repository,
            JournalStep.JobListing,
            state.RunId,
            state,
            (journal, run, entry, ct) => journal.SaveJobListingRunAsync(run, entry, ct),
            options,
            time,
            logger);
    }

    public long RunId
    {
        get
        {
            return core.RunId;
        }
    }

    /// <summary>The run as it stands; changed only inside <c>core.Change</c>.</summary>
    private JobListingJournalRun Run
    {
        get
        {
            return core.State;
        }
        set
        {
            core.State = value;
        }
    }

    public void Report(JobListingFetchProgress value)
    {
        if (value.Result is not { } result)
        {
            return; // the result for the same page follows at once
        }

        core.Change(now =>
        {
            var outcome = JobListingJournalOutcome.Kept;
            var read = 0;
            var kept = 1;
            if (result.Success)
            {
                outcome = JobListingJournalOutcome.Read;
                read = 1;
                kept = 0;
            }
            Run = Run with
            {
                LastSeenAt = now,
                Pages = value.Total,
                PagesRead = Run.PagesRead + read,
                PagesKept = Run.PagesKept + kept,
                Message = $"Page {value.Index} of {value.Total} read",
            };
            core.Save(new JobListingJournalEntry(0, now, value.Index, value.Total, value.File, outcome, result.Jobs, result.NewJobs, result.Message));
        });
    }

    public async Task CompleteAsync(JobListingFetchResult result)
    {
        core.Change(now =>
        {
            var index = 0;
            foreach (var file in result.Files)
            {
                index++;
                if (!file.Success)
                {
                    continue;
                }
                if (file.ArchivedTo is not null)
                {
                    core.Save(new JobListingJournalEntry(0, now, index, result.Files.Count, file.File, JobListingJournalOutcome.Archived, file.Jobs, file.NewJobs, file.ArchivedTo));
                }
                else
                {
                    core.Save(new JobListingJournalEntry(0, now, index, result.Files.Count, file.File, JobListingJournalOutcome.NotArchived, file.Jobs, file.NewJobs, file.Message));
                }
            }

            Run = Run with
            {
                LastSeenAt = now,
                FinishedAt = now,
                State = JournalRunState.Completed,
                Pages = result.Files.Count,
                JobsStored = result.Batch?.Count ?? 0,
                NewJobs = result.Files.Sum(f => f.NewJobs),
                ListingId = result.ListingId,
                Message = Summary(result),
            };
            core.Save(null);
        });
        await core.FinishAsync();
    }

    public async Task FailAsync(Exception error)
    {
        core.Change(now =>
        {
            Run = Run with
            {
                LastSeenAt = now,
                FinishedAt = now,
                State = JournalRunState.Failed,
                Message = error.GetBaseException().Message,
            };
            core.Save(null);
        });
        await core.FinishAsync();
    }

    /// <summary>A run disposed without a result ended unexpectedly: recorded as Failed.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!core.Finished)
        {
            await FailAsync(new InvalidOperationException("The run ended without reporting a result."));
        }
    }

    private static string Summary(JobListingFetchResult result)
    {
        if (result.Files.Count == 0)
        {
            return "No saved pages in the input folder. Nothing new to add.";
        }
        var read = result.Files.Count(f => f.Success);
        var kept = result.Files.Count - read;
        if (result.ListingId is null)
        {
            return $"{result.Files.Count} page(s), none could be read; nothing stored. The pages stay in the input folder.";
        }
        return $"{result.Files.Count} page(s): {read} read, {kept} kept. {result.Batch?.Count ?? 0} job links stored as listing {result.ListingId}.";
    }
}
