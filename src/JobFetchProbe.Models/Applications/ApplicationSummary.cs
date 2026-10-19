using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Models.Applications;

/// <summary>One application in the Applications list.</summary>
/// <param name="DaysSinceApplied">Whole days from the day of applying to today (local time).</param>
/// <param name="Flag">The follow-up reminder; always None unless the stage is Applied.</param>
/// <param name="FlagFrom">At stage Applied, the day the Follow up flag appears (applied + 7 days); otherwise null.</param>
/// <param name="LastNote">The owner's most recent note, or null when there is none.</param>
/// <param name="NoteCount">How many notes the owner wrote (the lines added by the app are not counted).</param>
/// <param name="Fit">The latest fit assessment of the job; null when it was never assessed.</param>
public sealed record ApplicationSummary(
    string JobId,
    string? Title,
    string? Company,
    string? Location,
    ApplicationStage Stage,
    DateTimeOffset AppliedAt,
    DateTimeOffset StageChangedAt,
    int DaysSinceApplied,
    FollowUpFlag Flag,
    DateOnly? FlagFrom,
    string? LastNote,
    int NoteCount,
    FitScore? Fit = null);
