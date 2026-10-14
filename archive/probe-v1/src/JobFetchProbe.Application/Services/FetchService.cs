using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Results;
using JobFetchProbe.Domain.Models;
using JobFetchProbe.Domain.Rules;

namespace JobFetchProbe.Application.Services;

public sealed class FetchService(IPageFetcher fetcher, IReportWriter reports, TimeProvider time) : IFetchService
{
    public async Task<ProbeResult> RunAsync(Uri jobUrl, CancellationToken ct = default)
    {
        // Re-checked here so the rule holds no matter which front end calls the service.
        var url = JobUrlRules.Normalize(jobUrl.OriginalString);

        var report = ReportFactory.Start("fetch", time);
        var warnings = new List<string>();

        report.Policy = reports.FindLastPolicy();
        if (report.Policy is null)
        {
            warnings.Add("no earlier policy run in the report directory; the lab note should record phase 0 first");
        }

        var fetch = await fetcher.GetAsync(url, "text/html", ct);
        ReportFactory.ApplyFetch(report, fetch);

        var transportFailed = fetch.TransportError is not null;
        var visible = transportFailed ? string.Empty : HtmlText.Visible(fetch.Body);
        var hasH1 = !transportFailed && HtmlText.HasH1(fetch.Body);
        // The body goes no further than this method.

        var result = ResponseClassifier.Classify(
            new ClassifierInput(fetch.Status, fetch.FinalUrl, visible, hasH1, transportFailed));

        report.Classification = result.Class.ToString();
        report.ClassificationReason = result.Reason;
        report.VisibleTextLength = visible.Length;
        report.Note = fetch.TransportError ?? fetch.StopReason;

        var status = result.Class switch
        {
            FetchClassification.DescriptionPresent => ProbeStatus.Completed,
            FetchClassification.Error when transportFailed => ProbeStatus.TransportFailed,
            _ => ProbeStatus.Stopped,
        };

        ReportFactory.Finish(report, status, time);
        reports.Write(report);

        return new ProbeResult(
            status,
            report,
            $"fetch host={fetch.FinalUrl.Host} status={fetch.Status} class={result.Class}",
            warnings);
    }
}
