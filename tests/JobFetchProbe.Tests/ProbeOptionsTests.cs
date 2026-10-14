namespace JobFetchProbe.Tests;

public class ProbeOptionsTests
{
    private const string GoodUrl = "https://www.linkedin.com/jobs/view/1234567890/?trk=public_jobs&refId=abc";

    [Fact]
    public void Fetch_without_live_is_rejected_with_spec_message()
    {
        var ex = Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["fetch", "--url", GoodUrl]));
        Assert.Equal("pass --live to send the single request", ex.Message);
    }

    [Fact]
    public void Fetch_with_live_strips_query_and_fragment()
    {
        var o = ProbeOptions.Parse(["fetch", "--url", GoodUrl + "#x", "--live"]);

        Assert.True(o.Live);
        Assert.Equal("https://www.linkedin.com/jobs/view/1234567890/", o.Url!.ToString());
    }

    [Theory]
    [InlineData("http://www.linkedin.com/jobs/view/1/")]
    [InlineData("https://linkedin.com/jobs/view/1/")]
    [InlineData("https://es.linkedin.com/jobs/view/1/")]
    [InlineData("https://user:pw@www.linkedin.com/jobs/view/1/")]
    [InlineData("https://www.linkedin.com:8443/jobs/view/1/")]
    [InlineData("https://www.linkedin.com/jobs/search/?keywords=dotnet")]
    [InlineData("https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/1")]
    [InlineData("https://www.linkedin.com/jobs/view/1/?start=25")]
    [InlineData("https://www.linkedin.com/jobs/view/1/?currentJobId=2")]
    [InlineData("https://www.linkedin.com/in/someone/")]
    [InlineData("not a url")]
    public void Out_of_scope_urls_are_rejected(string url)
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["fetch", "--url", url, "--live"]));
    }

    [Fact]
    public void Second_url_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            ProbeOptions.Parse(["fetch", "--url", GoodUrl, "--url", GoodUrl, "--live"]));
    }

    [Theory]
    [InlineData("4")]
    [InlineData("31")]
    [InlineData("abc")]
    public void Timeout_out_of_range_is_rejected(string value)
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["policy", "--timeout-seconds", value]));
    }

    [Fact]
    public void Saved_extractor_is_not_part_of_this_build()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ProbeOptions.Parse(["extract", "--html", TestPaths.Fixture("sample-job.html"), "--extractor", "saved"]));
        Assert.Equal("saved extractor is not part of this build", ex.Message);
    }

    [Fact]
    public void Flags_from_other_commands_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["policy", "--live"]));
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["extract", "--url", GoodUrl]));
    }
}
