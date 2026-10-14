using JobFetchProbe.Application.Dtos;
using JobFetchProbe.Application.Results;
using JobFetchProbe.Application.Services;

namespace JobFetchProbe.Application.Tests;

public class PolicyServiceTests
{
    private readonly InMemoryReportWriter _reports = new();

    private PolicyService Service(FakeFetcher fetcher) => new(fetcher, _reports, TimeProvider.System);

    [Fact]
    public async Task Disallow_all_stops_and_stores_rules()
    {
        var fetcher = new FakeFetcher(u => FakeFetcher.Response(u, 200, Samples.RobotsDisallowAll));

        var result = await Service(fetcher).RunAsync();

        Assert.Equal(ProbeStatus.Stopped, result.Status);
        Assert.Equal("policy general-client=disallow-all job-paths=disallowed", result.Summary);
        var (url, accept) = Assert.Single(fetcher.Calls);
        Assert.Equal(PolicyService.RobotsUrl, url);
        Assert.Equal("text/plain", accept);

        var report = Assert.Single(_reports.Written);
        Assert.Equal("policy", report.Command);
        Assert.True(report.Policy!.GeneralClientDisallowsAll);
        Assert.Equal(new[] { "Disallow: /" }, report.Policy.GeneralClientRules);
        Assert.Null(report.Posting);
    }

    [Fact]
    public async Task Allow_all_completes()
    {
        var fetcher = new FakeFetcher(u => FakeFetcher.Response(u, 200, Samples.RobotsAllowAll));

        var result = await Service(fetcher).RunAsync();

        Assert.Equal(ProbeStatus.Completed, result.Status);
        Assert.False(result.Report.Policy!.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public async Task Non_200_stops_without_policy()
    {
        var fetcher = new FakeFetcher(u => FakeFetcher.Response(u, 403, "denied"));

        var result = await Service(fetcher).RunAsync();

        Assert.Equal(ProbeStatus.Stopped, result.Status);
        Assert.Null(result.Report.Policy);
        Assert.Equal(403, result.Report.HttpStatus);
    }

    [Fact]
    public async Task Transport_failure_is_reported()
    {
        var fetcher = new FakeFetcher(u => new FetchResult
        {
            RequestedUrl = u,
            FinalUrl = u,
            RequestsSent = 1,
            TransportError = "timeout",
        });

        var result = await Service(fetcher).RunAsync();

        Assert.Equal(ProbeStatus.TransportFailed, result.Status);
        Assert.Null(result.Report.HttpStatus);
        Assert.Single(_reports.Written);
    }
}
