using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Stores the job descriptions of a listing batch as one &lt;BatchId&gt;.json.</summary>
public interface IDetailsRepository
{
    /// <summary>The details file of an earlier run for this batch, to resume from; null if none.</summary>
    Task<JobDetailsBatch?> LoadAsync(string batchId, CancellationToken ct = default);

    /// <summary>Writes (overwrites) the details file for the batch.</summary>
    /// <returns>Full path of the file.</returns>
    Task<string> SaveAsync(JobDetailsBatch details, CancellationToken ct = default);
}
