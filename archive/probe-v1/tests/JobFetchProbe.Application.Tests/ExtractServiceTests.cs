using JobFetchProbe.Application.Results;
using JobFetchProbe.Application.Services;
using JobFetchProbe.Domain.Models;

namespace JobFetchProbe.Application.Tests;

public class ExtractServiceTests
{
    private readonly InMemoryReportWriter _reports = new();

    [Fact]
    public void Posting_goes_into_report_and_summary()
    {
        var posting = JobPosting.Create("Backend engineer", "Northwind Tools", "Valencia",
            "Builds HTTP APIs in C# and runs them on Azure.", "sample-job.html");
        var service = new ExtractService(new FakeExtractor(_ => posting), _reports, TimeProvider.System);

        var result = service.Run("sample-job.html");

        Assert.Equal(ProbeStatus.Completed, result.Status);
        Assert.Same(posting, Assert.Single(_reports.Written).Posting);
        Assert.Contains("title=\"Backend engineer\"", result.Summary);
        Assert.False(result.Report.LiveRequestSent);
    }

    [Fact]
    public void Extractor_format_error_propagates_and_writes_nothing()
    {
        var service = new ExtractService(
            new FakeExtractor(_ => throw new FormatException("description is missing or empty")),
            _reports,
            TimeProvider.System);

        Assert.Throws<FormatException>(() => service.Run("bad.html"));
        Assert.Empty(_reports.Written);
    }
}
