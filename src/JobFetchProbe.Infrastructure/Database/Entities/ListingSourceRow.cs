namespace JobFetchProbe.Infrastructure.Database.Entities;

/// <summary>One saved page that contributed to a listing.</summary>
public sealed class ListingSourceRow
{
    public int Id { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string File { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? Query { get; set; }
    public int? TotalResults { get; set; }
    public int Jobs { get; set; }
    public int NewJobs { get; set; }
}
