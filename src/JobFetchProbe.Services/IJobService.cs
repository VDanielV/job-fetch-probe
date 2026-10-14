using JobFetchProbe.Models;

namespace JobFetchProbe.Services;

/// <summary>Reads a job from a link or a saved page and stores it.</summary>
public interface IJobService
{
    Task<JobResult> FromUrlAsync(Uri url, CancellationToken ct = default);

    Task<JobResult> FromFileAsync(string path, CancellationToken ct = default);
}
