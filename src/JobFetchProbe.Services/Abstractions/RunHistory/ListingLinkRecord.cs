using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Services.Abstractions.RunHistory;

/// <summary>One job link of a listing, with the job as it is now.</summary>
/// <param name="Position">Order in the listing (0-based, as stored).</param>
/// <param name="RetrievedAt">When its description was read, if it was.</param>
/// <param name="LastAttemptAt">When step 2 last tried it, if ever.</param>
public sealed record ListingLinkRecord(
    long JobId,
    int Position,
    string Source,
    string SourceJobId,
    string? Title,
    string? Company,
    DateTimeOffset FirstSeenAt,
    string Status,
    DateTimeOffset? RetrievedAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? AppliedAt,
    FitScore? Fit);
