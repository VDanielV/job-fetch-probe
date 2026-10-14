using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Console.Tests;

/// <summary>Scripted sessions against an in-memory fetcher. No network.</summary>
public class InteractiveShellTests
{
    private const string JobUrl = "https://www.linkedin.com/jobs/view/1234567890/";

    private sealed class CountingFetcher : IPageFetcher
    {
        public List<Uri> Calls { get; } = [];

        public Task<FetchResult> GetAsync(Uri url, string accept, CancellationToken ct = default)
        {
            Calls.Add(url);
            var body = url.AbsolutePath == "/robots.txt" ? "User-agent: *\nDisallow: /\n" : "<html>blocked</html>";
            return Task.FromResult(new FetchResult
            {
                Status = url.AbsolutePath == "/robots.txt" ? 200 : 999,
                RequestedUrl = url,
                FinalUrl = url,
                Body = body,
                BodyLength = body.Length,
                RequestsSent = 1,
            });
        }
    }

    private static async Task<(int Exit, string Out, string Err, CountingFetcher Fetcher)> Session(params string[] lines)
    {
        var dir = Path.Combine(Path.GetTempPath(), "jfp-tests-" + Guid.NewGuid().ToString("N"));
        var fetcher = new CountingFetcher();
        var script = string.Join("\n", lines.Select(l => l.Replace("{out}", dir)));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await new InteractiveShell(
            new StringReader(script), stdout, stderr,
            services => services.AddSingleton<IPageFetcher>(fetcher)).RunAsync();

        return (exit, stdout.ToString(), stderr.ToString(), fetcher);
    }

    [Fact]
    public async Task Help_and_exit()
    {
        var (exit, stdout, _, fetcher) = await Session("help", "exit", "policy");

        Assert.Equal(0, exit);
        Assert.Contains("fetch --url <job-view-url> --live", stdout);
        Assert.Empty(fetcher.Calls); // nothing after exit runs
    }

    [Fact]
    public async Task End_of_input_ends_the_session()
    {
        var (exit, stdout, _, _) = await Session("help");

        Assert.Equal(0, exit);
        Assert.Contains(InteractiveShell.Prompt, stdout);
    }

    [Fact]
    public async Task Commands_run_and_show_exit_codes()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-job.html");
        var (_, stdout, _, _) = await Session($"extract --html \"{sample}\" --out {{out}}");

        Assert.Contains("title=\"Backend engineer\"", stdout);
        Assert.Contains("(exit code 0)", stdout);
    }

    [Fact]
    public async Task Second_fetch_in_a_session_is_refused()
    {
        var (_, stdout, stderr, fetcher) = await Session(
            $"fetch --url {JobUrl} --live --out {{out}}",
            $"fetch --url {JobUrl} --live --out {{out}}",
            "limits");

        Assert.Single(fetcher.Calls);
        Assert.Contains("already used", stderr);
        Assert.Contains("fetch: used", stdout);
    }

    [Fact]
    public async Task Second_policy_in_a_session_is_refused()
    {
        var (_, _, stderr, fetcher) = await Session("policy --out {out}", "policy --out {out}");

        Assert.Single(fetcher.Calls);
        Assert.Contains("policy request for this session is already used", stderr);
    }

    [Fact]
    public async Task Rejected_arguments_do_not_use_the_request()
    {
        var (_, stdout, stderr, fetcher) = await Session(
            $"fetch --url {JobUrl} --out {{out}}",
            $"fetch --url {JobUrl} --live --out {{out}}");

        Assert.Contains("pass --live to send the single request", stderr);
        Assert.Contains("(exit code 2)", stdout);
        Assert.Single(fetcher.Calls);
    }

    [Fact]
    public async Task Unclosed_quote_is_reported_and_session_continues()
    {
        var (_, stdout, stderr, _) = await Session("extract --html \"C:\\x", "limits");

        Assert.Contains("missing closing", stderr);
        Assert.Contains("policy: available", stdout);
    }
}
