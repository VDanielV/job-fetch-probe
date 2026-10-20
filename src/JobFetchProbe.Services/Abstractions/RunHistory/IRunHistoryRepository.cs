namespace JobFetchProbe.Services.Abstractions.RunHistory;

/// <summary>What the history of past acquisitions is built from. Implemented by the data layer; it only reads.</summary>
public interface IRunHistoryRepository
{
    /// <summary>Every step 1 and step 2 run in the journal (without their entries), and every listing with its counts.</summary>
    Task<RunHistorySource> GetSourceAsync(CancellationToken ct = default);

    /// <summary>A listing's pages and job links, each job with what it is now; null when the listing is unknown.</summary>
    Task<ListingLinksRecord?> GetListingLinksAsync(string listingId, CancellationToken ct = default);
}
