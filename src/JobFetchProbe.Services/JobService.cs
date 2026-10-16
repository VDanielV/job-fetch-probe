using JobFetchProbe.Models.Jobs;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Parsing;

namespace JobFetchProbe.Services;

public sealed class JobService(IPageFetcher fetcher, IJobPageParser parser, TimeProvider time) : IJobService
{
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
                $"{page.FinalUrl.Host} did not return the job page (status {page.Status}: sign-in or refusal)") with { HttpStatus = page.Status };
        }
        if (page.Error is not null)
        {
            return JobResult.Failed($"could not fetch the page: {page.Error}") with { HttpStatus = page.Status };
        }

        var jobId = JobIdResolver.FromUrl(url)
                    ?? JobIdResolver.FromHtml(page.Html)
                    ?? $"job-{time.GetUtcNow():yyyyMMdd-HHmmss}";
        var job = parser.Parse(page.Html, jobId, url) with { RetrievedAt = time.GetUtcNow() };
        return JobResult.Read(job) with { HttpStatus = page.Status };
    }
}
