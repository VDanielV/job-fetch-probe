using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Models.RunHistory;

/// <summary>One job link of a run's listing: where it was found, whether it was new, what it is now, and what step 2 did with it.</summary>
/// <param name="Position">Order in the listing (1-based).</param>
/// <param name="Page">The saved page it was first found on in this run; null when unknown.</param>
/// <param name="IsNew">First seen in this run; false when the job was already in the database.</param>
/// <param name="Status">The job's status now: Pending, Read, Failed, Blocked, Gone or External.</param>
/// <param name="ArchivedAt">When it was archived, if it is.</param>
/// <param name="AppliedAt">When the owner applied, if he did.</param>
/// <param name="Fit">Its latest fit score, if any.</param>
/// <param name="Step2">What step 2 did with it in this run.</param>
/// <param name="Step2Text">The same in words, e.g. "Read · Fit 78 · Apply" or "Not asked for: already read on 16 Oct".</param>
public sealed record RunJobLink(
    long JobId,
    int Position,
    string? Page,
    string Source,
    string SourceJobId,
    string? Title,
    string? Company,
    bool IsNew,
    DateTimeOffset FirstSeenAt,
    string Status,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? AppliedAt,
    FitScore? Fit,
    RunJobStep2Kind Step2,
    string Step2Text);
