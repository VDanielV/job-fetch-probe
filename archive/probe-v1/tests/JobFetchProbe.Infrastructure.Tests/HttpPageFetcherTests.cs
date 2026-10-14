using System.Net;
using JobFetchProbe.Infrastructure.Http;

namespace JobFetchProbe.Infrastructure.Tests;

/// <summary>Runs the real fetcher against an in-memory handler. No network.</summary>
public class HttpPageFetcherTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static readonly Uri JobUrl = new("https://www.linkedin.com/jobs/view/1/");

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location) } };

    [Fact]
    public async Task Sends_identity_and_no_cookie_or_authorization()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });

        var result = await new HttpPageFetcher(new HttpClient(handler)).GetAsync(JobUrl, "text/html");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpPageFetcher.UserAgent, string.Join(" ", request.Headers.GetValues("User-Agent")));
        Assert.Equal("text/html", request.Headers.Accept.ToString());
        Assert.False(request.Headers.Contains("Cookie"));
        Assert.Null(request.Headers.Authorization);
        Assert.Equal(200, result.Status);
        Assert.Equal(1, result.RequestsSent);
    }

    [Fact]
    public async Task Redirect_to_authwall_is_recorded_and_not_followed()
    {
        var handler = new StubHandler(_ => Redirect("https://www.linkedin.com/authwall?trk=x"));

        var result = await new HttpPageFetcher(new HttpClient(handler)).GetAsync(JobUrl, "text/html");

        Assert.Single(handler.Requests);
        Assert.Equal(302, result.Status);
        Assert.Equal("/authwall", result.FinalUrl.AbsolutePath);
        Assert.False(Assert.Single(result.Hops).Followed);
        Assert.NotNull(result.StopReason);
    }

    [Fact]
    public async Task Redirect_off_linkedin_is_not_followed()
    {
        var handler = new StubHandler(_ => Redirect("https://example.com/elsewhere"));

        var result = await new HttpPageFetcher(new HttpClient(handler)).GetAsync(JobUrl, "text/html");

        Assert.Single(handler.Requests);
        Assert.Equal("example.com", result.FinalUrl.Host);
    }

    [Fact]
    public async Task At_most_three_redirects_are_followed()
    {
        var n = 0;
        var handler = new StubHandler(_ => Redirect($"https://www.linkedin.com/jobs/view/{++n + 1}/"));

        var result = await new HttpPageFetcher(new HttpClient(handler)).GetAsync(JobUrl, "text/html");

        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(4, result.Hops.Count);
        Assert.False(result.Hops[^1].Followed);
        Assert.Equal(4, result.RequestsSent);
    }

    [Fact]
    public async Task Transport_exception_becomes_status_zero_without_retry()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));

        var result = await new HttpPageFetcher(new HttpClient(handler)).GetAsync(JobUrl, "text/html");

        Assert.Single(handler.Requests);
        Assert.Equal(0, result.Status);
        Assert.NotNull(result.TransportError);
    }
}
