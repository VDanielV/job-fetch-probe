namespace JobFetchProbe.Data.Fit.Entities;

/// <summary>One fit assessment of a job by the local model. Never updated: a new check adds a new row.</summary>
public sealed class JobFitAssessmentRow
{
    public long Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public DateTimeOffset AssessedAt { get; set; }

    /// <summary>0 to 100.</summary>
    public int Fit { get; set; }

    /// <summary>A <c>JobFitDecision</c>, stored by name.</summary>
    public string Decision { get; set; } = string.Empty;

    public bool HardSkip { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>The model's list of missing facts, as a JSON array ("[]" when empty).</summary>
    public string MissingInformation { get; set; } = "[]";

    public string? Model { get; set; }

    /// <summary>A short hash of the Fit files the assessment was made with.</summary>
    public string ProfileVersion { get; set; } = string.Empty;
}
