using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Results;

namespace JobFetchProbe.Application.Services;

public sealed class ExtractService(IJobExtractor extractor, IReportWriter reports, TimeProvider time) : IExtractService
{
    public ProbeResult Run(string htmlPath)
    {
        var report = ReportFactory.Start("extract", time);
        var posting = extractor.Extract(htmlPath);
        report.Posting = posting;

        ReportFactory.Finish(report, ProbeStatus.Completed, time);
        reports.Write(report);

        var summary =
            $"extract source={posting.SourcePath} title=\"{posting.Title}\" company=\"{posting.Company}\" " +
            $"location=\"{posting.Location}\" description-chars={posting.Description.Length}";
        return new ProbeResult(ProbeStatus.Completed, report, summary);
    }
}
