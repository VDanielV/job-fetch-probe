using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// What the details pane shows for a job's fit: a score just made, or the latest stored one, or why there is none; with
/// the earlier stored scores below it.
/// </summary>
/// <param name="Seconds">How long the local model took for a score just made, as measured by the web app; -1 for a stored score.</param>
/// <param name="Model">The model that made the stored score.</param>
/// <param name="OlderProfile">The score was made with other Fit files than the current ones.</param>
/// <param name="Earlier">The older stored scores, newest first.</param>
/// <param name="CurrentProfile">The version of the Fit files now, to mark older scores; null when unknown.</param>
/// <param name="ArchiveHint">The page has the archive box, whose note the card fills in.</param>
public sealed record FitView(
    long JobId,
    JobFitResult? Result,
    string? Error,
    int Seconds,
    string? Model = null,
    bool OlderProfile = false,
    IReadOnlyList<JobFitAssessment>? Earlier = null,
    string? CurrentProfile = null,
    bool ArchiveHint = true)
{
    /// <summary>The latest stored score, with the others as earlier ones; null when the job was never scored.</summary>
    public static FitView? FromHistory(long jobId, JobFitHistory history)
    {
        if (history.Assessments.Count == 0)
        {
            return null;
        }
        var latest = history.Assessments[0];
        var result = new JobFitResult(latest.Decision, latest.Fit, latest.Reason, latest.HardSkip, latest.MissingInformation, latest.AssessedAt);
        return new FitView(
            jobId, result, null, -1, latest.Model, IsOlder(latest, history.CurrentProfileVersion), history.Assessments.Skip(1).ToList(),
            history.CurrentProfileVersion);
    }

    /// <summary>This view with the stored scores other than its own as the earlier ones.</summary>
    public FitView WithHistory(JobFitHistory history)
    {
        var earlier = history.Assessments
            .Where(a => Result?.AssessedAt is null || a.AssessedAt != Result.AssessedAt)
            .ToList();
        return this with { Earlier = earlier, CurrentProfile = history.CurrentProfileVersion };
    }

    /// <summary>The stored score was made with other Fit files than the current ones.</summary>
    public static bool IsOlder(JobFitAssessment assessment, string? current)
    {
        return current is not null && assessment.ProfileVersion != current;
    }

    /// <summary>The note offered to the archive box: "Fit 46% · Skip: the reason".</summary>
    public string? ArchiveNote
    {
        get
        {
            if (Result is null)
            {
                return null;
            }
            var note = $"Fit {Result.Fit}% · {Result.Decision}: {Result.Reason}";
            if (note.Length > 500)
            {
                note = note[..499] + "…";
            }
            return note;
        }
    }

    /// <summary>The CSS colour of the bar: green for Apply, amber for Maybe, red for Skip (or a hard skip).</summary>
    public string Tone
    {
        get
        {
            if (Result is null)
            {
                return "error";
            }
            return ToneOf(Result.Decision, Result.HardSkip);
        }
    }

    public static string ToneOf(JobFitDecision decision, bool hardSkip)
    {
        if (hardSkip || decision == JobFitDecision.Skip)
        {
            return "skip";
        }
        if (decision == JobFitDecision.Maybe)
        {
            return "maybe";
        }
        return "apply";
    }
}
