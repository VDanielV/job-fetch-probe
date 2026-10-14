using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Stores jobs. Implemented in Infrastructure as one JSON file per job.</summary>
public interface IJobRepository
{
    /// <returns>Where the job was stored (the full file path).</returns>
    Task<string> SaveAsync(JobPosting job, CancellationToken ct = default);
}
