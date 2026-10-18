using JobFetchProbe.Models.FetchJobListing;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Stores what step 1 read: the listing, its pages and its job links. Implemented with the database.</summary>
public interface IBatchRepository
{
    /// <returns>The listing's ID as stored (yyyyMMdd_HHmmss, with _2, _3… if two runs share a second).</returns>
    Task<string> SaveAsync(JobSearchBatch batch, CancellationToken ct = default);
}
