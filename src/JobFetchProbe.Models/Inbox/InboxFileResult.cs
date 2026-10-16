namespace JobFetchProbe.Models.Inbox;

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
