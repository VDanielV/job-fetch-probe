namespace JobFetchProbe.Services.Abstractions.RunHistory;

/// <summary>A stored listing and its counts.</summary>
/// <param name="Pages">Saved pages the listing was read from.</param>
/// <param name="Links">Job links in it.</param>
/// <param name="New">Of those, jobs first seen by this listing (their FirstSeenAt is the listing's CreatedAt).</param>
public sealed record ListingTally(string ListingId, DateTimeOffset CreatedAt, int Pages, int Links, int New);
