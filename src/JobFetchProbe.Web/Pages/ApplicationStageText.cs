using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Web.Pages;

/// <summary>How the stages and follow-up flags read on the pages.</summary>
public static class ApplicationStageText
{
    /// <summary>The stages that keep an application in the list.</summary>
    public static IReadOnlyList<ApplicationStage> Open { get; } = [ApplicationStage.Applied, ApplicationStage.Interview, ApplicationStage.Offer];

    /// <summary>The stages that close it.</summary>
    public static IReadOnlyList<ApplicationStage> Closed { get; } = [ApplicationStage.Rejected, ApplicationStage.Withdrawn, ApplicationStage.NoAnswer];

    public static bool IsClosed(ApplicationStage stage)
    {
        return Closed.Contains(stage);
    }

    public static string Label(ApplicationStage stage)
    {
        if (stage == ApplicationStage.NoAnswer)
        {
            return "No answer";
        }
        return stage.ToString();
    }

    /// <summary>"Follow up · 9 days", "No answer · 23 days", or null without a flag.</summary>
    public static string? Flag(ApplicationSummary application)
    {
        if (application.Flag == FollowUpFlag.FollowUp)
        {
            return $"Follow up · {application.DaysSinceApplied} days";
        }
        if (application.Flag == FollowUpFlag.NoAnswer)
        {
            return $"No answer · {application.DaysSinceApplied} days";
        }
        return null;
    }

    /// <summary>"today", "yesterday", "12 days".</summary>
    public static string Age(int days)
    {
        if (days == 0)
        {
            return "today";
        }
        if (days == 1)
        {
            return "yesterday";
        }
        return $"{days} days";
    }

    /// <summary>A date as "26 Oct".</summary>
    public static string Day(DateOnly day)
    {
        return day.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A stored time as "26 Oct", in this computer's time zone.</summary>
    public static string Day(DateTimeOffset at)
    {
        return Day(DateOnly.FromDateTime(at.ToLocalTime().DateTime));
    }
}
