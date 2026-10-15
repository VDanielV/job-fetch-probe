namespace JobFetchProbe.Models;

/// <summary>Outcome of one inbox run.</summary>
/// <param name="Batch">The merged jobs, or null when no file was read successfully.</param>
/// <param name="SavedTo">Full path of the batch file, or null when nothing was saved.</param>
/// <param name="Error">Set when the batch file could not be written; then no file is archived.</param>
public sealed record InboxRunResult(
    IReadOnlyList<InboxFileResult> Files,
    JobSearchBatch? Batch,
    string? SavedTo,
    string? Error)
{
    public static InboxRunResult Empty { get; } = new([], null, null, null);
}

/// <summary>Reported while the inbox step runs: before a page is read (Result null) and after (Result set).</summary>
public sealed record InboxProgress(int Index, int Total, string File, InboxFileResult? Result);

/// <summary>What happened to one inbox file.</summary>
/// <param name="Success">Read with at least one job. Only successful files are archived.</param>
/// <param name="ArchivedTo">Where the file was moved, or null if it stayed in the inbox.</param>
/// <param name="Message">Why the file failed, or why it could not be archived.</param>
public sealed record InboxFileResult(
    string File,
    bool Success,
    int Jobs,
    int NewJobs,
    string? ArchivedTo,
    string? Message);
