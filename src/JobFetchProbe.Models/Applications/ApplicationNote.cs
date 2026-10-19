namespace JobFetchProbe.Models.Applications;

/// <summary>One line of an application's timeline.</summary>
/// <param name="Text">The owner's note, "Marked as applied", or a stage change such as "Applied → Interview".</param>
public sealed record ApplicationNote(long Id, DateTimeOffset At, ApplicationNoteKind Kind, string Text);
