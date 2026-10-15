using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Stores inbox batches. Implemented in Infrastructure as &lt;BatchId&gt;.json.</summary>
public interface IBatchRepository
{
    /// <returns>Where the batch was stored (the full file path).</returns>
    Task<string> SaveAsync(JobSearchBatch batch, CancellationToken ct = default);
}
