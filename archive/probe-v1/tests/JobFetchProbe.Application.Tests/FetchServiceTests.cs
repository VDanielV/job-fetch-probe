using System.Text.Json;
using JobFetchProbe.Application.Dtos;
using JobFetchProbe.Application.Results;
using JobFetchProbe.Application.Services;

namespace JobFetchProbe.Application.Tests;

public class FetchServiceTests
{
    private const string BodyMarker = "INVENTED-BODY-MARKER-7f3a";
    private readonly InMemoryReportWriter _reports = new();

    private FetchService Service(FakeFetcher fetcher) => new(fetcher, _reports, TimeProvider.System);

    [Fact]
    public async Task Status_999_is_denied_and_report_holds_no_body_or_path()
    {
        var fetcher = new FakeFetcher(u => FakeFetcher.Response(u, 999, $"<html>{BodyMarker}</html>"));

        var result = await Service(fetcher).RunAsync(new Uri(Samples.JobUrl + "?trk=x"));

        Assert.Equal(ProbeStatus.Stopped, result.Status);
        Assert.Equal("fetch host=www.linkedin.com status=999 class=Denied", result.Summary);
        var (url, accept) = Assert.Single(fetcher.Calls);
        Assert.Equal(Samples.JobUrl, url);
        Assert.Equal("text/html", accept);

        var json = JsonSerializer.Serialize(Assert.Single(_reports.Written));
        Assert.DoesNotContain(BodyMarker, json);
        Assert.DoesNotContain("1234567890", json);
    }

    [Fact]
    public async Task Long_page_with_h1_completes()
    {
        var fetcher = new FakeFetcher(u => FakeFetcher.Response(u, 200, Samples.LongPage));

        var result = await Service(fetcher).RunAsync(Samples.JobUrl);

        Assert.Equal(ProbeStatus.Completed, result.Status);
        Assert.Equal("DescriptionPresent", result.Report.Classification);
    }

    [Fact]
    public async Task Redirect_to_authwall_is_auth_wall()
    {
        var fetcher = new FakeFetcher(u => FakeFetcher.Response(u, 302, string.Empty, new Uri("https://www.linkedin.com/authwall")));

        var result = await Service(fetcher).RunAsync(Samples.JobUrl);

        Assert.Equal(ProbeStatus.Stopped, result.Status);
        Assert.Equal("AuthWall", result.Report.Classification);
    }

    [Fact]
    public async Task Transport_failure_is_transport_failed()
    {
        var fetcher = new FakeFetcher(u => new FetchResult
        {
            RequestedUrl = u,
            FinalUrl = u,
            RequestsSent = 1,
            TransportError = "timeout",
        });

        var result = await Service(fetcher).RunAsync(Samples.JobUrl);

        Assert.Equal(ProbeStatus.TransportFailed, result.Status);
        Assert.Equal("Error", result.Report.Classification);
    }

    [Fact]
    public async Task Earlier_policy_is_copied_and_missing_policy_warns()
    {
        var fetcher = new FakeFetcher(u => FakeFetcher.Response(u, 999, string.Empty));

        var withoutPolicy = await Service(fetcher).RunAsync(Samples.JobUrl);
        Assert.Single(withoutPolicy.Warnings);

        _reports.LastPolicy = new PolicySummary { JobPathsDisallowedForGeneralClient = true };
        var withPolicy = await Service(fetcher).RunAsync(Samples.JobUrl);
        Assert.Empty(withPolicy.Warnings);
        Assert.True(withPolicy.Report.Policy!.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public async Task Out_of_scope_url_sends_nothing()
    {
        var fetcher = new FakeFetcher(u => throw new InvalidOperationException("no request expected"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(fetcher).RunAsync(new Uri("https://www.linkedin.com/jobs/search/?keywords=x")));
        Assert.Empty(fetcher.Calls);
        Assert.Empty(_reports.Written);
    }
}
