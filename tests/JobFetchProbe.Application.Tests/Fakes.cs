using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Dtos;
using JobFetchProbe.Domain.Models;

namespace JobFetchProbe.Application.Tests;

internal sealed class FakeFetcher(Func<Uri, FetchResult> respond) : IPageFetcher
{
    public List<(Uri Url, string Accept)> Calls { get; } = [];

    public Task<FetchResult> GetAsync(Uri url, string accept, CancellationToken ct = default)
    {
        Calls.Add((url, accept));
        return Task.FromResult(respond(url));
    }

    public static FetchResult Response(Uri url, int status, string body, Uri? final = null) => new()
    {
        Status = status,
        RequestedUrl = url,
        FinalUrl = final ?? url,
        ContentType = "text/html",
        Body = body,
        BodyLength = body.Length,
        RequestsSent = 1,
    };
}

internal sealed class InMemoryReportWriter : IReportWriter
{
    public List<ProbeReport> Written { get; } = [];
    public PolicySummary? LastPolicy { get; set; }

    public void Write(ProbeReport report) => Written.Add(report);

    public PolicySummary? FindLastPolicy() => LastPolicy;
}

internal sealed class FakeExtractor(Func<string, JobPosting> extract) : IJobExtractor
{
    public JobPosting Extract(string htmlPath) => extract(htmlPath);
}

internal static class Samples
{
    public const string RobotsDisallowAll = "User-agent: ExampleBot\nDisallow: /jobs-guest/\n\nUser-agent: *\nDisallow: /\n";
    public const string RobotsAllowAll = "User-agent: *\nAllow: /\n";
    public static readonly Uri JobUrl = new("https://www.linkedin.com/jobs/view/1234567890/");

    public static readonly string LongPage =
        "<html><body><h1>Invented role</h1><p>" +
        string.Join(' ', Enumerable.Repeat("Invented description sentence for the service test.", 12)) +
        "</p></body></html>";
}
