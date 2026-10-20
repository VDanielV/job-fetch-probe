namespace JobFetchProbe.Data.Entities;

/// <summary>A labelled fact from the job page, e.g. "Tipo de empleo: Jornada completa".</summary>
public sealed class JobDetailRow
{
    public int Id { get; set; }
    /// <summary>The job (<c>Jobs.Id</c>).</summary>
    public long JobId { get; set; }
    public int Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
