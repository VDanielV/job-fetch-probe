using JobFetchProbe.Application.Dtos;

namespace JobFetchProbe.Application.Abstractions;

public interface IReportWriter
{
    /// <summary>Overwrites the current report and appends one line to the run log.</summary>
    void Write(ProbeReport report);

    /// <summary>The two policy booleans from the most recent successful policy run, if any.</summary>
    PolicySummary? FindLastPolicy();
}
