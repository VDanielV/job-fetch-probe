using JobFetchProbe.Domain.Rules;

namespace JobFetchProbe.Domain.Tests;

public class JobUrlRulesTests
{
    [Fact]
    public void Valid_url_loses_query_and_fragment()
    {
        var url = JobUrlRules.Normalize("https://www.linkedin.com/jobs/view/1234567890/?trk=public_jobs&refId=abc#x");

        Assert.Equal("https://www.linkedin.com/jobs/view/1234567890/", url.ToString());
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
        Assert.Throws<ArgumentException>(() => JobUrlRules.Normalize(url));
    }
}
