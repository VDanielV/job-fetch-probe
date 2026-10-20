using System.Globalization;
using JobFetchProbe.Models.RunHistory;

namespace JobFetchProbe.Web.Pages;

/// <summary>How the Runs pages write a past acquisition: dates, duration, state, and the colour of step 2's outcome.</summary>
public static class RunText
{
    /// <summary>"Sun 18 Oct 17:48", in local time.</summary>
    public static string When(DateTimeOffset at)
    {
        return at.ToLocalTime().ToString("ddd d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>"18 Oct 15:41", in local time.</summary>
    public static string Day(DateTimeOffset at)
    {
        return at.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>From the start of step 1 to the end of the acquisition; empty when unknown.</summary>
    public static string Duration(RunHistoryLine run)
    {
        if (run.RunId is null || run.FinishedAt is not { } end)
        {
            return string.Empty;
        }
        return AcquisitionView.Duration(end - run.StartedAt);
    }

    /// <summary>Completed, Stopped, Failed…; "No journal" for a listing stored before the journal existed.</summary>
    public static string State(RunHistoryLine run)
    {
        if (run.State is { } state)
        {
            return state.ToString();
        }
        return "No journal";
    }

    /// <summary>The CSS class of the state tag.</summary>
    public static string StateClass(RunHistoryLine run)
    {
        if (run.State is { } state)
        {
            return state.ToString();
        }
        return "NoJournal";
    }

    /// <summary>The CSS class of the outcome tag, reusing the status colours.</summary>
    public static string Step2Class(RunJobStep2Kind kind)
    {
        switch (kind)
        {
            case RunJobStep2Kind.Read:
                return "Read";
            case RunJobStep2Kind.Refused:
                return "Blocked";
            case RunJobStep2Kind.Failed:
                return "Failed";
            case RunJobStep2Kind.NotReached:
                return "Pending";
            default:
                return "Gone";
        }
    }

    /// <summary>The outcome tag's word.</summary>
    public static string Step2Tag(RunJobStep2Kind kind)
    {
        switch (kind)
        {
            case RunJobStep2Kind.Read:
                return "Read";
            case RunJobStep2Kind.Refused:
                return "Refused";
            case RunJobStep2Kind.Failed:
                return "Failed";
            case RunJobStep2Kind.Gone:
                return "Gone";
            case RunJobStep2Kind.NotAsked:
                return "Not asked";
            case RunJobStep2Kind.NotReached:
                return "Not reached";
            default:
                return "Unknown";
        }
    }

    /// <summary>The outcome's text without the tag's word, e.g. "already read on 16 Oct 13:50".</summary>
    public static string Step2Detail(RunJobLink link)
    {
        var text = link.Step2Text;
        foreach (var prefix in new[] { "Not asked for: ", "Not reached: ", "Not known: ", "Gone: ", "Failed: " })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return text[prefix.Length..];
            }
        }
        if (text.StartsWith("Read · ", StringComparison.Ordinal))
        {
            return text["Read · ".Length..];
        }
        if (text.StartsWith("Refused", StringComparison.Ordinal))
        {
            return text["Refused".Length..].TrimStart(':', ' ');
        }
        if (text == "Read" || text == "Not reached")
        {
            return string.Empty;
        }
        return text;
    }
}
