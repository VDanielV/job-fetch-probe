namespace JobFetchProbe.Models.Applications;

/// <summary>One application with its whole timeline, newest line first.</summary>
public sealed record Application(ApplicationSummary Summary, IReadOnlyList<ApplicationNote> Notes);
