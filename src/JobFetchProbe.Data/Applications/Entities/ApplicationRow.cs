namespace JobFetchProbe.Data.Applications.Entities;

/// <summary>A job the owner applied to. The job itself stays in Jobs, unchanged.</summary>
public sealed class ApplicationRow
{
    /// <summary>The job (<c>Jobs.Id</c>).</summary>
    public long JobId { get; set; }
    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>An <c>ApplicationStage</c>, stored by name.</summary>
    public string Stage { get; set; } = string.Empty;

    public DateTimeOffset StageChangedAt { get; set; }

    public List<ApplicationNoteRow> Notes { get; set; } = [];
}
