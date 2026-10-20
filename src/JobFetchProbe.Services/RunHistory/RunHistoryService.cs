using JobFetchProbe.Models.Journal;
using JobFetchProbe.Models.RunHistory;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Abstractions.Journal;
using JobFetchProbe.Services.Abstractions.RunHistory;
using JobFetchProbe.Services.Catalog;

namespace JobFetchProbe.Services.RunHistory;

/// <inheritdoc />
public sealed class RunHistoryService(IRunHistoryRepository history, IJournalRepository journal, IJobCatalog catalog) : IRunHistoryService
{
    public async Task<RunHistoryPage> GetPageAsync(int page, int pageSize, bool includeEmpty, CancellationToken ct = default)
    {
        var all = await AllAsync(ct);
        var emptyRuns = all.Count(l => l.Empty);
        var shown = all;
        if (!includeEmpty)
        {
            shown = all.Where(l => !l.Empty).ToList();
        }
        var lines = shown.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new RunHistoryPage(lines, page, pageSize, shown.Count, emptyRuns);
    }

    public async Task<RunHistoryLine?> GetRunAsync(long runId, CancellationToken ct = default)
    {
        var all = await AllAsync(ct);
        return all.FirstOrDefault(l => l.RunId == runId);
    }

    public async Task<RunJobLinks?> GetJobLinksAsync(long runId, CancellationToken ct = default)
    {
        var line = await GetRunAsync(runId, ct);
        if (line is null)
        {
            return null;
        }
        if (line.ListingId is null)
        {
            return new RunJobLinks(line, []);
        }
        JobDetailsJournalRun? details = null;
        if (line.DetailsRunId is { } detailsId)
        {
            details = await journal.GetJobDetailsRunAsync(detailsId, 0, ct);
        }
        return await LinksAsync(line, line.ListingId, details, hasJournal: true, ct);
    }

    public async Task<RunJobLinks?> GetListingJobLinksAsync(string listingId, CancellationToken ct = default)
    {
        var all = await AllAsync(ct);
        var line = all.FirstOrDefault(l => l.RunId is null && l.ListingId == listingId);
        if (line is null)
        {
            return null;
        }
        return await LinksAsync(line, listingId, null, hasJournal: false, ct);
    }

    private async Task<RunJobLinks?> LinksAsync(RunHistoryLine line, string listingId, JobDetailsJournalRun? details, bool hasJournal, CancellationToken ct)
    {
        var record = await history.GetListingLinksAsync(listingId, ct);
        if (record is null)
        {
            return new RunJobLinks(line, []);
        }
        var pages = ListingPages.Of(record.Pages);
        var links = new List<RunJobLink>();
        foreach (var link in record.Links)
        {
            var (kind, text) = Step2Outcome.Of(link, details, hasJournal);
            links.Add(new RunJobLink(
                link.JobId, link.Position + 1, ListingPages.PageAt(pages, link.Position), link.Source, link.SourceJobId,
                link.Title, link.Company, link.FirstSeenAt == record.CreatedAt, link.FirstSeenAt, link.Status,
                link.ArchivedAt, link.AppliedAt, link.Fit, kind, text));
        }
        return new RunJobLinks(line, links);
    }

    private async Task<List<RunHistoryLine>> AllAsync(CancellationToken ct)
    {
        if (!catalog.IsAvailable)
        {
            throw new JobStoreUnavailableException();
        }
        var source = await history.GetSourceAsync(ct);
        return RunPairing.Build(source);
    }
}
