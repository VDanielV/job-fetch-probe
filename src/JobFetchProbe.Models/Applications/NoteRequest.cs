namespace JobFetchProbe.Models.Applications;

/// <summary>A note to add to an application's timeline, dated now. 1 to 2000 characters.</summary>
public sealed record NoteRequest(string? Text);
