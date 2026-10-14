using JobFetchProbe.Infrastructure.Extraction;

namespace JobFetchProbe.Infrastructure.Tests;

public class FixtureJobExtractorTests
{
    private const string Template =
        """
        <article data-probe="job">
          <h1 data-field="title">Backend engineer</h1>
          <p data-field="company">Northwind Tools</p>
          <p data-field="location">Valencia</p>
          {0}
        </article>
        """;

    [Fact]
    public void Valid_fixture_returns_the_four_invented_fields()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-job.html");

        var posting = new FixtureJobExtractor().Extract(path);

        Assert.Equal("Backend engineer", posting.Title);
        Assert.Equal("Northwind Tools", posting.Company);
        Assert.Equal("Valencia", posting.Location);
        Assert.Equal("Builds HTTP APIs in C# and runs them on Azure.", posting.Description);
        Assert.Equal("sample-job.html", posting.SourcePath);
    }

    [Fact]
    public void Missing_description_throws_with_field_name()
    {
        var html = string.Format(Template, string.Empty);

        var ex = Assert.Throws<FormatException>(() => FixtureJobExtractor.ExtractFromHtml(html, "x.html"));
        Assert.Contains("description", ex.Message);
    }

    [Fact]
    public void Whitespace_only_description_throws_with_field_name()
    {
        var html = string.Format(Template, "<section data-field=\"description\">   \n\t   </section>");

        var ex = Assert.Throws<FormatException>(() => FixtureJobExtractor.ExtractFromHtml(html, "x.html"));
        Assert.Contains("description", ex.Message);
    }

    [Fact]
    public void Multiline_description_is_collapsed()
    {
        var html = string.Format(Template, "<section data-field=\"description\">\n  Builds HTTP APIs in C#\n  and runs them on Azure.\n</section>");

        var posting = FixtureJobExtractor.ExtractFromHtml(html, "x.html");

        Assert.Equal("Builds HTTP APIs in C# and runs them on Azure.", posting.Description);
    }

    [Fact]
    public void Malformed_markup_throws_format_exception()
    {
        Assert.Throws<FormatException>(() => FixtureJobExtractor.ExtractFromHtml("<html><body><p>unclosed", "saved.html"));
    }
}
