using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Services.Applications;

/// <summary>
/// The follow-up flags, in one place: only at stage Applied, Follow up from day <see cref="FollowUpDays"/>, No answer
/// from day <see cref="NoAnswerDays"/>, counted in whole local days from the day of applying.
/// </summary>
internal static class FollowUpRules
{
    public const int FollowUpDays = 7;
    public const int NoAnswerDays = 21;

    /// <summary>Whole days from the day of applying to <paramref name="today"/>; 0 on the same day, never negative.</summary>
    public static int DaysSince(DateTimeOffset appliedAt, DateTimeOffset today)
    {
        var from = DateOnly.FromDateTime(appliedAt.ToOffset(today.Offset).DateTime);
        var to = DateOnly.FromDateTime(today.DateTime);
        return Math.Max(0, to.DayNumber - from.DayNumber);
    }

    public static FollowUpFlag Flag(ApplicationStage stage, int days)
    {
        if (stage != ApplicationStage.Applied)
        {
            return FollowUpFlag.None;
        }
        if (days >= NoAnswerDays)
        {
            return FollowUpFlag.NoAnswer;
        }
        if (days >= FollowUpDays)
        {
            return FollowUpFlag.FollowUp;
        }
        return FollowUpFlag.None;
    }

    /// <summary>At stage Applied, the day the Follow up flag appears; null at any other stage.</summary>
    public static DateOnly? FlagFrom(ApplicationStage stage, DateTimeOffset appliedAt, DateTimeOffset today)
    {
        if (stage != ApplicationStage.Applied)
        {
            return null;
        }
        return DateOnly.FromDateTime(appliedAt.ToOffset(today.Offset).DateTime).AddDays(FollowUpDays);
    }
}
