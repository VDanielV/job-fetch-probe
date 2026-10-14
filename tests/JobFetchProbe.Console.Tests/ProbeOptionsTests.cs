using JobFetchProbe.Console.Cli;

namespace JobFetchProbe.Console.Tests;

public class ProbeOptionsTests
{
    private const string GoodUrl = "https://www.linkedin.com/jobs/view/1234567890/?trk=public_jobs";
    private static string SampleJob => Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-job.html");

    [Fact]
    public void Fetch_without_live_is_rejected_with_spec_message()
    {
        var ex = Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(new[] { "fetch", "--url", GoodUrl }));
        Assert.Equal("pass --live to send the single request", ex.Message);
    }

    [Fact]
    public void Fetch_with_live_strips_query()
    {
        var o = ProbeOptions.Parse(new[] { "fetch", "--url", GoodUrl, "--live" });

        Assert.True(o.Live);
        Assert.Equal("https://www.linkedin.com/jobs/view/1234567890/", o.Url!.ToString());
    }

    [Fact]
    public void Second_url_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            ProbeOptions.Parse(new[] { "fetch", "--url", GoodUrl, "--url", GoodUrl, "--live" }));
    }

    [Theory]
    [InlineData("4")]
    [InlineData("31")]
    [InlineData("abc")]
    public void Timeout_out_of_range_is_rejected(string value)
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(new[] { "policy", "--timeout-seconds", value }));
    }

    [Fact]
    public void Saved_extractor_is_not_part_of_this_build()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ProbeOptions.Parse(new[] { "extract", "--html", SampleJob, "--extractor", "saved" }));
        Assert.Equal("saved extractor is not part of this build", ex.Message);
    }

    [Fact]
    public void Flags_from_other_commands_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(new[] { "policy", "--live" }));
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(new[] { "extract", "--url", GoodUrl }));
    }

    [Fact]
    public void Unknown_command_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(new[] { "crawl" }));
    }
}
