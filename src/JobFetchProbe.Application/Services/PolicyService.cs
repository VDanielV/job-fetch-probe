using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Dtos;
using JobFetchProbe.Application.Results;
using JobFetchProbe.Domain.Rules;

namespace JobFetchProbe.Application.Services;

public sealed class PolicyService(IPageFetcher fetcher, IReportWriter reports, TimeProvider time) : IPolicyService
{
    public static readonly Uri RobotsUrl = new("https://www.linkedin.com/robots.txt");

    public async Task<ProbeResult> RunAsync(CancellationToken ct = default)
    {
        var report = ReportFactory.Start("policy", time);
        var fetch = await fetcher.GetAsync(RobotsUrl, "text/plain", ct);
        ReportFactory.ApplyFetch(report, fetch);

        if (fetch.TransportError is not null)
        {
            report.Note = fetch.TransportError;
            return Done(report, ProbeStatus.TransportFailed, $"policy status=0 error={fetch.TransportError}");
        }

        if (fetch.Status != 200)
        {
            report.Note = $"robots.txt returned status {fetch.Status}; no policy parsed";
            return Done(report, ProbeStatus.Stopped, $"policy status={fetch.Status} parsed=no");
        }

        var policy = RobotsPolicyParser.Analyze(fetch.Body);
        report.Policy = new PolicySummary
        {
            GeneralClientDisallowsAll = policy.GeneralClientDisallowsAll,
            JobPathsDisallowedForGeneralClient = policy.JobPathsDisallowedForGeneralClient,
            GeneralClientRules = policy.GeneralClientRules,
            JobRuleLines = policy.JobRuleLines,
        };

        var disallowed = policy.GeneralClientDisallowsAll || policy.JobPathsDisallowedForGeneralClient;
        if (disallowed)
        {
            report.Note = "job paths are disallowed for a general client; phase 2 stays off unless the operator passes --live after reading this";
        }

        var summary =
            $"policy general-client={(policy.GeneralClientDisallowsAll ? "disallow-all" : "not-disallow-all")} " +
            $"job-paths={(policy.JobPathsDisallowedForGeneralClient ? "disallowed" : "not-disallowed")}";

        return Done(report, disallowed ? ProbeStatus.Stopped : ProbeStatus.Completed, summary);
    }

    private ProbeResult Done(ProbeReport report, ProbeStatus status, string summary)
    {
        ReportFactory.Finish(report, status, time);
        reports.Write(report);
        return new ProbeResult(status, report, summary);
    }
}
