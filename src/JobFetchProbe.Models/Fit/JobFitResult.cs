namespace JobFetchProbe.Models.Fit;

/// <summary>The local model's score for one stored job. The API keeps every score (GET api/JobFit/{jobId}).</summary>
/// <param name="Decision">Apply, Maybe, or Skip. A hard skip is always Skip.</param>
/// <param name="Fit">0 to 100, from the scoring rubric.</param>
/// <param name="Reason">One short sentence.</param>
/// <param name="HardSkip">True when a hard rule matched. The fit number is still reported.</param>
/// <param name="MissingInformation">Facts the posting did not give and that mattered. Empty when nothing important was missing.</param>
/// <param name="AssessedAt">When the answer came back; null when it is not known.</param>
public sealed record JobFitResult(
    JobFitDecision Decision,
    int Fit,
    string Reason,
    bool HardSkip,
    IReadOnlyList<string> MissingInformation,
    DateTimeOffset? AssessedAt = null);
