namespace JobFetchProbe.Data.Entities;

/// <summary>A job as its card showed it in one listing (the card may differ from the job page).</summary>
public sealed class ListingJobRow
{
    public string BatchId { get; set; } = string.Empty;
    /// <summary>The job (<c>Jobs.Id</c>).</summary>
    public long JobId { get; set; }
    public int Position { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? ListedAt { get; set; }
}
