namespace JobFetchProbe.Services.Abstractions;

/// <summary>What storing step 1's listing did.</summary>
/// <param name="ListingId">The listing's ID as stored (yyyyMMdd_HHmmss, with _2, _3… if two runs share a second).</param>
/// <param name="NewJobs">Jobs added to the database: not seen in any earlier run.</param>
public sealed record BatchSaveResult(string ListingId, int NewJobs);
