namespace JobFetchProbe.Services.Fit;

/// <summary>The three Fit files, already read. The service does not open them itself.</summary>
public sealed record FitPack(string Profile, string Policy, string Rubric);
