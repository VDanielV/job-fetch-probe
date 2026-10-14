using JobFetchProbe.Application.Dtos;

namespace JobFetchProbe.Application.Results;

/// <param name="Summary">One short console line, e.g. <c>fetch host=www.linkedin.com status=999 class=Denied</c>.</param>
/// <param name="Warnings">Notes for the operator; never response content.</param>
public sealed record ProbeResult(ProbeStatus Status, ProbeReport Report, string Summary, IReadOnlyList<string> Warnings)
{
    public ProbeResult(ProbeStatus status, ProbeReport report, string summary)
        : this(status, report, summary, [])
    {
    }
}
