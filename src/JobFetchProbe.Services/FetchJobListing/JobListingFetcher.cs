using JobFetchProbe.Models.FetchJobListing;
using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Models.Search;
using JobFetchProbe.Services.Abstractions;

namespace JobFetchProbe.Services.FetchJobListing;

/// <summary>
/// Step 1, FetchJobListing:
/// 1. read each saved search page in the input folder, one by one (no network);
/// 2. merge their jobs, dropping any job already found in an earlier page of the run;
/// 3. if at least one page was read, store the listing and its job links in the database;
/// 4. only after that, move each page that was read to the archive.
/// Pages that fail stay in the input folder, with the reason reported, and are tried again on the next run.
/// If the database cannot be written, the exception ends the run and nothing is archived.
/// </summary>
public sealed class JobListingFetcher(
    IInputFolder input,
    ISearchService search,
    IBatchRepository repository,
    TimeProvider time) : IJobListingFetcher
{
    public const string BatchIdFormat = "yyyyMMdd_HHmmss";

    public async Task<JobListingFetchResult> FetchAsync(IProgress<JobListingFetchProgress>? progress = null, CancellationToken ct = default)
    {
        var pending = input.ListPending();
        if (pending.Count == 0)
        {
            return JobListingFetchResult.Empty;
        }

        var files = new List<JobListingFileResult>();
        var sources = new List<BatchSource>();
        var jobs = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var readPaths = new List<(string Path, int Index)>();

        for (var n = 0; n < pending.Count; n++)
        {
            // A stop request ends the step between two pages: nothing is stored and no page is archived, so every
            // page stays in the input folder for the next run.
            if (ct.IsCancellationRequested)
            {
                return new JobListingFetchResult(files, null, null, Stopped: true);
            }
            var path = pending[n];
            var name = Path.GetFileName(path);
            progress?.Report(new JobListingFetchProgress(n + 1, pending.Count, name, null));
            SearchResult read;
            try
            {
                read = await search.ReadFileAsync(path, ct);
            }
            catch (OperationCanceledException)
            {
                return new JobListingFetchResult(files, null, null, Stopped: true);
            }

            if (read.Status != JobResultStatus.Read || read.Search is null)
            {
                files.Add(new JobListingFileResult(name, false, 0, 0, null, read.Message));
                progress?.Report(new JobListingFetchProgress(n + 1, pending.Count, name, files[^1]));
                continue;
            }

            var added = 0;
            foreach (var job in read.Search.Jobs)
            {
                if (seen.Add(job.SourceJobId))
                {
                    jobs.Add(job);
                    added++;
                }
            }

            sources.Add(new BatchSource(name, read.Search.SourceUrl, read.Search.Query, read.Search.TotalResults, read.Search.Count, added));
            readPaths.Add((path, files.Count));
            files.Add(new JobListingFileResult(name, true, read.Search.Count, added, null, null));
            progress?.Report(new JobListingFetchProgress(n + 1, pending.Count, name, files[^1]));
        }

        if (sources.Count == 0)
        {
            return new JobListingFetchResult(files, null, null);
        }

        var now = time.GetLocalNow();
        var batch = new JobSearchBatch
        {
            BatchId = now.ToString(BatchIdFormat),
            CreatedAt = now,
            Sources = sources,
            Jobs = jobs,
        };

        if (ct.IsCancellationRequested)
        {
            return new JobListingFetchResult(files, null, null, Stopped: true);
        }

        // From here the step completes: the listing is stored in one transaction and the pages are archived.
        var saved = await repository.SaveAsync(batch, CancellationToken.None);

        foreach (var (path, index) in readPaths)
        {
            try
            {
                files[index] = files[index] with { ArchivedTo = input.Archive(path) };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                files[index] = files[index] with { Message = $"read and saved, but could not be archived: {ex.Message}" };
            }
        }

        return new JobListingFetchResult(files, batch, saved.ListingId, NewJobs: saved.NewJobs);
    }
}
