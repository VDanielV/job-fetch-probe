using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Search;

namespace JobFetchProbe.Services.Inbox;

/// <summary>
/// One inbox run:
/// 1. read each pending page, one by one, with the search step's reader (no network);
/// 2. merge their jobs, dropping any job already found in an earlier page of the run;
/// 3. if at least one page was read, write the batch as &lt;yyyyMMdd_HHmmss&gt;.json;
/// 4. only after the batch is written, move each page that was read to the archive.
/// Pages that fail stay in the inbox, with the reason reported, and are tried again on the next run.
/// </summary>
public sealed class InboxService(
    IInboxFolder inbox,
    ISearchService search,
    IBatchRepository repository,
    TimeProvider time) : IInboxService
{
    public const string BatchIdFormat = "yyyyMMdd_HHmmss";

    public async Task<InboxRunResult> RunAsync(IProgress<InboxProgress>? progress = null, CancellationToken ct = default)
    {
        var pending = inbox.ListPending();
        if (pending.Count == 0)
        {
            return InboxRunResult.Empty;
        }

        var files = new List<InboxFileResult>();
        var sources = new List<BatchSource>();
        var jobs = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var readPaths = new List<(string Path, int Index)>();

        for (var n = 0; n < pending.Count; n++)
        {
            ct.ThrowIfCancellationRequested();
            var path = pending[n];
            var name = Path.GetFileName(path);
            progress?.Report(new InboxProgress(n + 1, pending.Count, name, null));
            var read = await search.ReadFileAsync(path, ct);

            if (read.Status != JobResultStatus.Read || read.Search is null)
            {
                files.Add(new InboxFileResult(name, false, 0, 0, null, read.Message));
                progress?.Report(new InboxProgress(n + 1, pending.Count, name, files[^1]));
                continue;
            }

            var added = 0;
            foreach (var job in read.Search.Jobs)
            {
                if (seen.Add(job.JobId))
                {
                    jobs.Add(job);
                    added++;
                }
            }

            sources.Add(new BatchSource(name, read.Search.SourceUrl, read.Search.Query, read.Search.TotalResults, read.Search.Count, added));
            readPaths.Add((path, files.Count));
            files.Add(new InboxFileResult(name, true, read.Search.Count, added, null, null));
            progress?.Report(new InboxProgress(n + 1, pending.Count, name, files[^1]));
        }

        if (sources.Count == 0)
        {
            return new InboxRunResult(files, null, null, null);
        }

        var now = time.GetLocalNow();
        var batch = new JobSearchBatch
        {
            BatchId = now.ToString(BatchIdFormat),
            CreatedAt = now,
            Sources = sources,
            Jobs = jobs,
        };

        string savedTo;
        try
        {
            savedTo = await repository.SaveAsync(batch, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing is archived, so the same pages are read again next time.
            return new InboxRunResult(files, batch, null, $"could not write the batch file: {ex.Message}");
        }

        foreach (var (path, index) in readPaths)
        {
            try
            {
                files[index] = files[index] with { ArchivedTo = inbox.Archive(path) };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                files[index] = files[index] with { Message = $"read and saved, but could not be archived: {ex.Message}" };
            }
        }

        return new InboxRunResult(files, batch, savedTo, null);
    }
}
