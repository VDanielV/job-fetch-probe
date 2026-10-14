using JobFetchProbe.Domain.Models;

namespace JobFetchProbe.Domain.Tests;

public class JobPostingTests
{
    private const string Description = "Builds HTTP APIs in C# and runs them on Azure.";

    [Fact]
    public void Create_trims_fields_and_keeps_file_name_only()
    {
        var posting = JobPosting.Create("  Backend engineer ", "Northwind Tools", "Valencia", Description, "/some/dir/page.html");

        Assert.Equal("Backend engineer", posting.Title);
        Assert.Equal("page.html", posting.SourcePath);
    }

    [Theory]
    [InlineData(null, "title")]
    [InlineData("   ", "title")]
    public void Missing_title_names_the_field(string? title, string field)
    {
        var ex = Assert.Throws<FormatException>(() => JobPosting.Create(title, "c", "l", Description, "x.html"));
        Assert.Contains(field, ex.Message);
    }

    [Fact]
    public void Location_may_be_unspecified()
    {
        var posting = JobPosting.Create("t", "c", "Unspecified", Description, "x.html");
        Assert.Equal("Unspecified", posting.Location);
    }

    [Fact]
    public void Description_under_40_chars_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(() => JobPosting.Create("t", "c", "l", "Too short.", "x.html"));
        Assert.Contains("description", ex.Message);
    }

    [Fact]
    public void Company_over_200_chars_is_rejected()
    {
        var ex = Assert.Throws<FormatException>(() => JobPosting.Create("t", new string('c', 201), "l", Description, "x.html"));
        Assert.Contains("company", ex.Message);
    }
}
