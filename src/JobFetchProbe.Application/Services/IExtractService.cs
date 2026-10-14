using JobFetchProbe.Application.Results;

namespace JobFetchProbe.Application.Services;

/// <summary>Phase 1: extract a job from a local HTML file. No network.</summary>
public interface IExtractService
{
    /// <exception cref="FormatException">The file does not satisfy the extractor's contract.</exception>
    ProbeResult Run(string htmlPath);
}
