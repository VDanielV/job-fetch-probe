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
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return JobResult.Failed("the link must start with https://");
        }

        var page = await fetcher.GetAsync(url, ct);
        if (page.Blocked)
        {
            return JobResult.Blocked(
                $"{page.FinalUrl.Host} did not return the job page (status {page.Status}: sign-in or refusal). " +
                "Save the page from your browser and give the file path instead.");
        }
        if (page.Error is not null)
        {
            return JobResult.Failed($"could not fetch the page: {page.Error}");
        }

        return await ParseAndSaveAsync(page.Html, url.ToString(), JobIdResolver.FromUrl(url), ct);
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

        return await ParseAndSaveAsync(html, null, JobIdResolver.FromHtml(html) ?? JobIdResolver.FromFileName(path), ct);
    }

    private async Task<JobResult> ParseAndSaveAsync(string html, string? sourceUrl, string? knownId, CancellationToken ct)
    {
        var parsed = parser.Parse(html);
        var job = parsed with
        {
            JobId = knownId ?? JobIdResolver.FromHtml(html) ?? $"job-{time.GetUtcNow():yyyyMMdd-HHmmss}",
            SourceUrl = sourceUrl ?? JobIdResolver.CanonicalUrl(html),
            RetrievedAt = time.GetUtcNow(),
        };

        var path = await repository.SaveAsync(job, ct);
        return JobResult.Saved(job, path);
    }
}
