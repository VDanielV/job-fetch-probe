using JobFetchProbe.Models;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Parsing;

namespace JobFetchProbe.Services;

public sealed class JobService(
    IPageFetcher fetcher,
    ISavedPageReader savedPages,
    IJobPageParser parser,
    IJobRepository repository,
    TimeProvider time) : IJobService
{
    public async Task<JobResult> FromUrlAsync(Uri url, CancellationToken ct = default)
    {
        var read = await ReadUrlAsync(url, ct);
        if (read.Status != JobResultStatus.Read || read.Job is null)
        {
            return read;
        }
        var path = await repository.SaveAsync(read.Job, ct);
        return JobResult.Saved(read.Job, path);
    }

    public async Task<JobResult> ReadUrlAsync(Uri url, CancellationToken ct = default)
    {
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return JobResult.Failed("the link must start with https://");
        }

        var page = await fetcher.GetAsync(url, ct);
        if (page.Blocked)
        {
            return JobResult.Blocked(
                $"{page.FinalUrl.Host} did not return the job page (status {page.Status}: sign-in or refusal). " +
                "Save the page from your browser and give the file path instead.") with { HttpStatus = page.Status };
        }
        if (page.Error is not null)
        {
            return JobResult.Failed($"could not fetch the page: {page.Error}") with { HttpStatus = page.Status };
        }

        return JobResult.Read(Parse(page.Html, url, JobIdResolver.FromUrl(url))) with { HttpStatus = page.Status };
    }

    public async Task<JobResult> FromFileAsync(string path, CancellationToken ct = default)
    {
        string html;
        try
        {
            html = await savedPages.ReadAsync(path, ct);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            return JobResult.Failed($"cannot read file: {ex.Message}");
        }

        // A saved page must say where it came from; without that the job could not be reached again.
        var sourceUrl = JobIdResolver.CanonicalUri(html);
        if (sourceUrl is null)
        {
            return JobResult.Failed(
                "the saved page does not contain its own address (no canonical link or og:url), so the job's URL is unknown. " +
                "Open the job's page in the browser and save it again, or paste the link instead.");
        }

        return await ParseAndSaveAsync(html, sourceUrl, JobIdResolver.FromHtml(html) ?? JobIdResolver.FromFileName(path), ct);
    }

    private async Task<JobResult> ParseAndSaveAsync(string html, Uri sourceUrl, string? knownId, CancellationToken ct)
    {
        var job = Parse(html, sourceUrl, knownId);
        var path = await repository.SaveAsync(job, ct);
        return JobResult.Saved(job, path);
    }

    private JobPosting Parse(string html, Uri sourceUrl, string? knownId)
    {
        var jobId = knownId
                    ?? JobIdResolver.FromHtml(html)
                    ?? $"job-{time.GetUtcNow():yyyyMMdd-HHmmss}";

        return parser.Parse(html, jobId, sourceUrl) with { RetrievedAt = time.GetUtcNow() };
    }
}
