using JobFetchProbe.Extract;

namespace JobFetchProbe.Tests;

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
        var posting = new FixtureJobExtractor().Extract(TestPaths.Fixture("sample-job.html"));

        Assert.Equal("Backend engineer", posting.Title);
        Assert.Equal("Northwind Tools", posting.Company);
        Assert.Equal("Valencia", posting.Location);
        Assert.Equal("Builds HTTP APIs in C# and runs them on Azure.", posting.Description);
        Assert.Equal("sample-job.html", posting.SourcePath);
    }

    [Fact]
    public void Source_path_is_file_name_only()
    {
        var html = string.Format(Template, "<section data-field=\"description\">Builds HTTP APIs in C# and runs them on Azure.</section>");
        var posting = FixtureJobExtractor.ExtractFromHtml(html, "/some/dir/inner/page.html");

        Assert.Equal("page.html", posting.SourcePath);
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
    public void Short_description_throws()
    {
        var html = string.Format(Template, "<section data-field=\"description\">Too short.</section>");

        var ex = Assert.Throws<FormatException>(() => FixtureJobExtractor.ExtractFromHtml(html, "x.html"));
        Assert.Contains("description", ex.Message);
    }

    [Fact]
    public void Overlong_title_throws()
    {
        var html = string.Format(Template, "<section data-field=\"description\">Builds HTTP APIs in C# and runs them on Azure.</section>")
            .Replace("Backend engineer", new string('a', 201));

        var ex = Assert.Throws<FormatException>(() => FixtureJobExtractor.ExtractFromHtml(html, "x.html"));
        Assert.Contains("title", ex.Message);
    }

    [Fact]
    public void Malformed_markup_throws_format_exception()
    {
        Assert.Throws<FormatException>(() => FixtureJobExtractor.ExtractFromHtml("<html><body><p>unclosed", "saved.html"));
    }
}
