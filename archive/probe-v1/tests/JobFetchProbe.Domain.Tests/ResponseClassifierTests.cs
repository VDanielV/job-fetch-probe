using JobFetchProbe.Domain.Models;
using JobFetchProbe.Domain.Rules;

namespace JobFetchProbe.Domain.Tests;

public class ResponseClassifierTests
{
    private static readonly Uri JobUrl = new("https://www.linkedin.com/jobs/view/1234567890/");
    private static readonly string LongText = string.Join(' ', Enumerable.Repeat("Invented description sentence for the classifier test.", 12));

    private static FetchClassification Classify(int status, string html, Uri? finalUrl = null, bool transportFailed = false) =>
        ResponseClassifier.Classify(new ClassifierInput(
            status,
            finalUrl ?? JobUrl,
            HtmlText.Visible(html),
            HtmlText.HasH1(html),
            transportFailed)).Class;

    [Fact]
    public void Transport_failure_is_error()
    {
        Assert.Equal(FetchClassification.Error, Classify(0, string.Empty, transportFailed: true));
    }

    [Fact]
    public void Status_zero_is_error()
    {
        Assert.Equal(FetchClassification.Error, Classify(0, string.Empty));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(999)]
    public void Block_statuses_are_denied(int status)
    {
        Assert.Equal(FetchClassification.Denied, Classify(status, "<html><body>blocked</body></html>"));
    }

    [Fact]
    public void Denied_wins_over_auth_url()
    {
        Assert.Equal(FetchClassification.Denied, Classify(999, string.Empty, new Uri("https://www.linkedin.com/authwall")));
    }

    [Theory]
    [InlineData("https://www.linkedin.com/login?x=1")]
    [InlineData("https://www.linkedin.com/uas/login")]
    [InlineData("https://www.linkedin.com/authwall?trk=a")]
    [InlineData("https://www.linkedin.com/checkpoint/challenge")]
    public void Auth_final_url_is_auth_wall(string finalUrl)
    {
        var html = $"<html><body><h1>Sign in</h1><p>{LongText}</p></body></html>";
        Assert.Equal(FetchClassification.AuthWall, Classify(200, html, new Uri(finalUrl)));
    }

    [Fact]
    public void Stopped_redirect_to_auth_is_auth_wall()
    {
        Assert.Equal(FetchClassification.AuthWall, Classify(302, string.Empty, new Uri("https://www.linkedin.com/authwall")));
    }

    [Theory]
    [InlineData("<html><body><h1>Let's do a quick Security Verification</h1></body></html>")]
    [InlineData("<html><body><div>Please complete the CAPTCHA</div></body></html>")]
    public void Challenge_text_is_challenge(string html)
    {
        Assert.Equal(FetchClassification.Challenge, Classify(200, html));
    }

    [Fact]
    public void Challenge_word_inside_script_is_not_visible()
    {
        var html = $"<html><head><script>var captcha = 1;</script></head><body><h1>Invented role</h1><p>{LongText}</p></body></html>";
        Assert.Equal(FetchClassification.DescriptionPresent, Classify(200, html));
    }

    [Fact]
    public void Short_page_is_empty_shell()
    {
        var html = "<html><head><style>body{}</style><script>" + new string('x', 5000) + "</script></head><body><h1>Title</h1><div id=\"root\"></div></body></html>";
        Assert.Equal(FetchClassification.EmptyShell, Classify(200, html));
    }

    [Fact]
    public void Empty_body_is_empty_shell()
    {
        Assert.Equal(FetchClassification.EmptyShell, Classify(200, string.Empty));
    }

    [Fact]
    public void Long_page_with_h1_is_description_present()
    {
        var html = $"<html><body><h1 class=\"top\">Invented role</h1><section>{LongText}</section></body></html>";
        Assert.Equal(FetchClassification.DescriptionPresent, Classify(200, html));
    }

    [Fact]
    public void Long_page_without_h1_falls_to_error()
    {
        var html = $"<html><body><h2>Invented role</h2><section>{LongText}</section></body></html>";
        Assert.Equal(FetchClassification.Error, Classify(200, html));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(302)]
    public void Other_status_is_error(int status)
    {
        Assert.Equal(FetchClassification.Error, Classify(status, $"<html><body><h1>x</h1>{LongText}</body></html>"));
    }

    [Fact]
    public void Visible_text_strips_script_style_comments_and_tags()
    {
        var html = "<p>a&amp;b</p><!-- hidden --><script>secret()</script><style>.x{}</style>  <b>c</b>";
        Assert.Equal("a&b c", HtmlText.Visible(html));
    }
}
