namespace JobFetchProbe.Services.Abstractions.RunHistory;

/// <summary>A listing's pages (in the order read) and job links (in the order stored).</summary>
public sealed record ListingLinksRecord(
    string ListingId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ListingPageRecord> Pages,
    IReadOnlyList<ListingLinkRecord> Links);
