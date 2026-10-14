using System.Text.Json;
using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Console.Tests;

/// <summary>
/// End-to-end runs through the real composition root, with only the fetcher replaced. No network.
/// </summary>
public class ProbeAppTests
{
    private const string JobUrl = "https://www.linkedin.com/jobs/view/1234567890/";

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

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jfp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<(int Exit, string Out, string Err)> Run(string dir, string[] args, IPageFetcher? fetcher = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await ProbeApp.RunAsync(
            [.. args, "--out", dir],
            stdout,
            stderr,
            fetcher is null ? null : services => services.AddSingleton(fetcher));
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private static JsonElement ReadReport(string dir) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "probe-report.json"))).RootElement;

    [Fact]
    public async Task Fetch_without_live_builds_no_services_and_exits_2()
    {
        var dir = NewTempDir();
        var configured = 0;
        var stderr = new StringWriter();

        var exit = await ProbeApp.RunAsync(
            new[] { "fetch", "--url", JobUrl, "--out", dir },
            TextWriter.Null,
            stderr,
            _ => configured++);

        Assert.Equal(2, exit);
        Assert.Equal(0, configured);
        Assert.Contains("pass --live to send the single request", stderr.ToString());
        Assert.False(File.Exists(Path.Combine(dir, "probe-report.json")));
    }

    [Fact]
    public async Task Extract_fixture_exits_0_and_reports_posting()
    {
        var dir = NewTempDir();
        var fetcher = new FakeFetcher(_ => throw new InvalidOperationException("extract must not fetch"));

        var (exit, stdout, _) = await Run(dir, new[] { "extract", "--html", Fixture("sample-job.html") }, fetcher);

        Assert.Equal(0, exit);
        Assert.Contains("title=\"Backend engineer\"", stdout);
        Assert.Equal("Backend engineer", ReadReport(dir).GetProperty("posting").GetProperty("title").GetString());
        Assert.Empty(fetcher.Calls);
    }

    [Fact]
    public async Task Policy_then_fetch_carries_policy_and_exits_3()
    {
        var dir = NewTempDir();
        var robots = new FakeFetcher(u => Response(u, 200, File.ReadAllText(Fixture("robots-sample.txt"))));
        var (policyExit, policyOut, _) = await Run(dir, new[] { "policy" }, robots);

        Assert.Equal(3, policyExit);
        Assert.Contains("policy general-client=disallow-all job-paths=disallowed", policyOut);

        var job = new FakeFetcher(u => Response(u, 999, "<html>blocked</html>"));
        var (fetchExit, fetchOut, fetchErr) = await Run(dir, new[] { "fetch", "--url", JobUrl, "--live" }, job);

        Assert.Equal(3, fetchExit);
        Assert.Contains("fetch host=www.linkedin.com status=999 class=Denied", fetchOut);
        Assert.DoesNotContain("note:", fetchErr);
        var report = ReadReport(dir);
        Assert.Equal("Denied", report.GetProperty("classification").GetString());
        Assert.True(report.GetProperty("policy").GetProperty("jobPathsDisallowedForGeneralClient").GetBoolean());
        Assert.Equal(2, File.ReadAllLines(Path.Combine(dir, "probe-log.jsonl")).Length);
    }

    [Fact]
    public async Task Transport_failure_exits_4_with_report()
    {
        var dir = NewTempDir();
        var fetcher = new FakeFetcher(u => new FetchResult
        {
            RequestedUrl = u,
            FinalUrl = u,
            RequestsSent = 1,
            TransportError = "timeout",
        });

        var (exit, _, _) = await Run(dir, new[] { "fetch", "--url", JobUrl, "--live" }, fetcher);

        Assert.Equal(4, exit);
        Assert.Equal("Error", ReadReport(dir).GetProperty("classification").GetString());
    }

    [Fact]
    public async Task Extract_of_non_contract_file_exits_2()
    {
        var dir = NewTempDir();
        var bad = Path.Combine(dir, "page.saved.html");
        await File.WriteAllTextAsync(bad, "<html><body><p>unclosed");

        var (exit, _, stderr) = await Run(dir, new[] { "extract", "--html", bad });

        Assert.Equal(2, exit);
        Assert.Contains("not well-formed", stderr);
    }

    [Fact]
    public void Real_composition_registers_every_service()
    {
        var services = new ServiceCollection();
        JobFetchProbe.Application.DependencyInjection.AddApplication(services);
        JobFetchProbe.Infrastructure.DependencyInjection.AddInfrastructure(services, new JobFetchProbe.Infrastructure.InfrastructureOptions());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<JobFetchProbe.Application.Services.IPolicyService>());
        Assert.NotNull(provider.GetRequiredService<JobFetchProbe.Application.Services.IExtractService>());
        Assert.NotNull(provider.GetRequiredService<JobFetchProbe.Application.Services.IFetchService>());
    }
}
