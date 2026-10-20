namespace JobFetchProbe.Data.Archive.Entities;

/// <summary>An archived job: the owner will not apply to it. The job itself stays in Jobs, unchanged.</summary>
public sealed class ArchivedJobRow
{
    /// <summary>The job (<c>Jobs.Id</c>).</summary>
    public long JobId { get; set; }
    public DateTimeOffset ArchivedAt { get; set; }

    /// <summary>An <c>ArchiveReason</c>, stored by name.</summary>
    public string Reason { get; set; } = string.Empty;

    public string? Note { get; set; }
}
