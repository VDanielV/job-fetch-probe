namespace JobFetchProbe.Services.Archive;

/// <summary>An archive request that breaks the rules (no job, too many, a bad reason, a missing or too long note). Nothing was archived.</summary>
public sealed class ArchiveRequestException(string field, string message) : Exception(message)
{
    /// <summary>The request field at fault: jobIds, reason or note.</summary>
    public string Field { get; } = field;
}
