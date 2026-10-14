using JobFetchProbe.Application.Results;

namespace JobFetchProbe.Application.Services;

/// <summary>Phase 2: one anonymous GET of one job URL, classified.</summary>
public interface IFetchService
{
    /// <exception cref="ArgumentException">The URL breaks the job URL rules. No request is sent.</exception>
    Task<ProbeResult> RunAsync(Uri jobUrl, CancellationToken ct = default);
}
