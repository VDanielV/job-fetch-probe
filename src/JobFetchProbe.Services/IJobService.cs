using JobFetchProbe.Models;

namespace JobFetchProbe.Services;

/// <summary>Reads a job from a link or a saved page and stores it.</summary>
public interface IJobService
{
    Task<JobResult> FromUrlAsync(Uri url, CancellationToken ct = default);

    Task<JobResult> FromFileAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Fetches and reads one job page without storing it. Returns status Read with the job on success,
    /// otherwise Blocked or Failed with a message. Used by the details step, which stores many jobs in one file.
    /// </summary>
    Task<JobResult> ReadUrlAsync(Uri url, CancellationToken ct = default);
}
