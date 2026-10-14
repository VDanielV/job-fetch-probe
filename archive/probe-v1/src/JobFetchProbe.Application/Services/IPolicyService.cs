using JobFetchProbe.Application.Results;

namespace JobFetchProbe.Application.Services;

/// <summary>Phase 0: read the published crawler policy.</summary>
public interface IPolicyService
{
    Task<ProbeResult> RunAsync(CancellationToken ct = default);
}
