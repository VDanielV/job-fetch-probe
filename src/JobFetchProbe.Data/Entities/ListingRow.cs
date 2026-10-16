namespace JobFetchProbe.Data.Entities;

/// <summary>One run of step 1: the pages read and the jobs they listed.</summary>
public sealed class ListingRow
{
    /// <summary>yyyyMMdd_HHmmss, with _2, _3… if two runs share a second.</summary>
    public string BatchId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<ListingSourceRow> Sources { get; set; } = [];
    public List<ListingJobRow> Jobs { get; set; } = [];
}
