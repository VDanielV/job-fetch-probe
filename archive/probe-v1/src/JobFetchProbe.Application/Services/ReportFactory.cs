using JobFetchProbe.Application.Dtos;
using JobFetchProbe.Application.Results;

namespace JobFetchProbe.Application.Services;

/// <summary>Shared report bookkeeping for the three services.</summary>
internal static class ReportFactory
{
    public static ProbeReport Start(string command, TimeProvider time)
    {
        var started = Truncate(time.GetUtcNow().UtcDateTime);
        return new ProbeReport
        {
            RunId = started.ToString("yyyyMMdd'T'HHmmss'Z'"),
            Command = command,
            StartedAt = started,
        };
    }

    public static void Finish(ProbeReport report, ProbeStatus status, TimeProvider time)
    {
        report.FinishedAt = Truncate(time.GetUtcNow().UtcDateTime);
        report.Outcome = status.ToString();
    }

    /// <summary>Copies transport facts into the report. The body is deliberately not copied.</summary>
    public static void ApplyFetch(ProbeReport report, FetchResult fetch)
    {
        report.RequestsSent = fetch.RequestsSent;
        report.LiveRequestSent = fetch.RequestsSent > 0;
        report.HttpStatus = fetch.Status == 0 ? null : fetch.Status;
        report.ContentType = fetch.ContentType;
        report.BodyLength = fetch.TransportError is null ? fetch.BodyLength : null;
        report.FinalUrlHost = fetch.FinalUrl.Host;
        report.Redirects = fetch.Hops;
    }

    private static DateTime Truncate(DateTime utc) =>
        new(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
}
