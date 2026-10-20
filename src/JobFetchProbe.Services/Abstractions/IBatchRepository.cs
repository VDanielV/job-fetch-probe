using JobFetchProbe.Models.FetchJobListing;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>Stores what step 1 read: the listing, its pages and its job links. Implemented with the database.</summary>
public interface IBatchRepository
{
    /// <returns>The listing's ID as stored, and how many of its jobs were new to the database.</returns>
    Task<BatchSaveResult> SaveAsync(JobSearchBatch batch, CancellationToken ct = default);
}
