using JobFetchProbe.Models.Fit;

namespace JobFetchProbe.Web.Pages;

/// <summary>What the details pane shows after "Check fit": the score, or why there is none. Never saved.</summary>
/// <param name="Seconds">How long the local model took, as measured by the web app.</param>
public sealed record FitView(string JobId, JobFitResult? Result, string? Error, int Seconds)
{
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

    /// <summary>The CSS colour of the bar: green from 60, amber from 40, red below (or for a hard skip).</summary>
    public string Tone
    {
        get
        {
            if (Result is null)
            {
                return "error";
            }
            if (Result.HardSkip || Result.Decision == JobFitDecision.Skip)
            {
                return "skip";
            }
            if (Result.Decision == JobFitDecision.Maybe)
            {
                return "maybe";
            }
            return "apply";
        }
    }
}
