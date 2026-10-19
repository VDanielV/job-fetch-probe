namespace JobFetchProbe.Services.Applications;

/// <summary>
/// The request cannot be done in the application's current state: the job is already applied to or archived, or an undo
/// would delete notes the owner wrote and was not confirmed. Nothing was changed.
/// </summary>
public sealed class ApplicationConflictException(string message, int notes = 0) : Exception(message)
{
    /// <summary>For an unconfirmed undo: how many notes the owner wrote. 0 otherwise.</summary>
    public int Notes { get; } = notes;
}
