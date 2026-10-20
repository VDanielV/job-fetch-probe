namespace JobFetchProbe.Data.Applications.Entities;

/// <summary>One line of an application's timeline.</summary>
public sealed class ApplicationNoteRow
{
    public long Id { get; set; }
    /// <summary>The job (<c>Jobs.Id</c>).</summary>
    public long JobId { get; set; }
    public DateTimeOffset At { get; set; }

    /// <summary>An <c>ApplicationNoteKind</c>, stored by name.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
