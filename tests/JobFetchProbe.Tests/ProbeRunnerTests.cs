using System.Text.Json;
using JobFetchProbe.Fetch;

namespace JobFetchProbe.Tests;

/// <summary>End-to-end runs against an in-memory fetcher. No network.</summary>
public class ProbeRunnerTests
{
    private const string JobUrl = "https://www.linkedin.com/jobs/view/1234567890/";
    private const string BodyMarker = "INVENTED-BODY-MARKER-7f3a";

    private sealed class FakeFetcher(Func<Uri, FetchResult> respond) : IPageFetcher
    {
        public List<(Uri Url, string Accept)> Calls { get; } = [];

        public Task<FetchResult> GetAsync(Uri url, string accept, CancellationToken ct = default)
        {
            Calls.Add((url, accept));
            return Task.FromResult(respond(url));
        }
    }

    private static FetchResult Response(Uri url, int status, string body, Uri? final = null) => new()
    {
        Status = status,
        RequestedUrl = url,
        FinalUrl = final ?? url,
        ContentType = "text/html",
        Body = body,
        BodyLength = body.Length,
        RequestsSent = 1,
    };

    private static async Task<(int Exit, JsonElement Report, string Out, string Err, string Dir)> Run(
        string[] args, Func<int, IPageFetcher> factory)
    {
        var dir = TestPaths.NewTempDir();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await ProbeRunner.RunAsync([.. args, "--out", dir], factory, stdout, stderr);
        var reportPath = Path.Combine(dir, "probe-report.json");
        var report = File.Exists(reportPath)
            ? JsonDocument.Parse(File.ReadAllText(reportPath)).RootElement
            : default;
        return (exit, report, stdout.ToString(), stderr.ToString(), dir);
    }

    [Fact]
    public async Task Fetch_without_live_sends_no_request_and_exits_2()
    {
        var factoryCalls = 0;
        var (exit, _, _, err, dir) = await Run(["fetch", "--url", JobUrl], _ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("no client may be created");
        });

        Assert.Equal(2, exit);
        Assert.Equal(0, factoryCalls);
        Assert.Contains("pass --live to send the single request", err);
        Assert.False(File.Exists(Path.Combine(dir, "probe-report.json")));
    }

    [Fact]
    public async Task Extract_fixture_exits_0_and_reports_posting()
    {
        var (exit, report, _, _, _) = await Run(
            ["extract", "--html", TestPaths.Fixture("sample-job.html")],
            _ => throw new InvalidOperationException("extract must not create a client"));

        Assert.Equal(0, exit);
        Assert.Equal("Backend engineer", report.GetProperty("posting").GetProperty("title").GetString());
        Assert.False(report.GetProperty("liveRequestSent").GetBoolean());
    }

    [Fact]
    public async Task Policy_with_disallow_all_exits_3_and_stores_rules()
    {
        var fetcher = new FakeFetcher(u => Response(u, 200, File.ReadAllText(TestPaths.Fixture("robots-sample.txt"))));
        var (exit, report, stdout, _, _) = await Run(["policy"], _ => fetcher);

        Assert.Equal(3, exit);
        Assert.Single(fetcher.Calls);
        Assert.Equal("text/plain", fetcher.Calls[0].Accept);
        var policy = report.GetProperty("policy");
        Assert.True(policy.GetProperty("generalClientDisallowsAll").GetBoolean());
        Assert.True(policy.GetProperty("jobPathsDisallowedForGeneralClient").GetBoolean());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("posting").ValueKind);
        Assert.Contains("policy general-client=disallow-all job-paths=disallowed", stdout);
    }

    [Fact]
    public async Task Fetch_999_is_denied_exits_3_and_persists_no_body()
    {
        var fetcher = new FakeFetcher(u => Response(u, 999, $"<html>{BodyMarker}</html>"));
        var (exit, report, stdout, _, dir) = await Run(["fetch", "--url", JobUrl + "?trk=x", "--live"], _ => fetcher);

        Assert.Equal(3, exit);
        Assert.Single(fetcher.Calls);
        Assert.Equal(JobUrl, fetcher.Calls[0].Url.ToString());
        Assert.Equal("text/html", fetcher.Calls[0].Accept);
        Assert.Equal("Denied", report.GetProperty("classification").GetString());
        Assert.Equal(999, report.GetProperty("httpStatus").GetInt32());
        Assert.Equal("www.linkedin.com", report.GetProperty("finalUrlHost").GetString());
        Assert.Contains("fetch host=www.linkedin.com status=999 class=Denied", stdout);

        foreach (var file in Directory.GetFiles(dir))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain(BodyMarker, text);
            Assert.DoesNotContain("1234567890", text);
        }
    }

    [Fact]
    public async Task Fetch_after_policy_carries_policy_booleans()
    {
        var dir = TestPaths.NewTempDir();
        var robots = new FakeFetcher(u => Response(u, 200, File.ReadAllText(TestPaths.Fixture("robots-sample.txt"))));
        await ProbeRunner.RunAsync(["policy", "--out", dir], _ => robots, TextWriter.Null, TextWriter.Null);

        var job = new FakeFetcher(u => Response(u, 302, string.Empty, new Uri("https://www.linkedin.com/authwall")));
        var exit = await ProbeRunner.RunAsync(["fetch", "--url", JobUrl, "--live", "--out", dir], _ => job, TextWriter.Null, TextWriter.Null);

        var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "probe-report.json"))).RootElement;
        Assert.Equal(3, exit);
        Assert.Equal("AuthWall", report.GetProperty("classification").GetString());
        Assert.True(report.GetProperty("policy").GetProperty("jobPathsDisallowedForGeneralClient").GetBoolean());
        Assert.Equal(2, File.ReadAllLines(Path.Combine(dir, "probe-log.jsonl")).Length);
    }

    [Fact]
    public async Task Transport_failure_exits_4_with_report()
    {
        var fetcher = new FakeFetcher(u => new FetchResult
        {
            Status = 0,
            RequestedUrl = u,
            FinalUrl = u,
            RequestsSent = 1,
            TransportError = "timeout",
        });
        var (exit, report, _, _, _) = await Run(["fetch", "--url", JobUrl, "--live"], _ => fetcher);

        Assert.Equal(4, exit);
        Assert.Equal("Error", report.GetProperty("classification").GetString());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("httpStatus").ValueKind);
    }
}
