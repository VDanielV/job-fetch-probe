using JobFetchProbe.Domain.Models;

namespace JobFetchProbe.Application.Dtos;

/// <summary>
/// Everything a run leaves on disk. Holds no response body, no headers, no cookies and no URL path.
/// </summary>
public sealed class ProbeReport
{
    public required string RunId { get; init; }
    public required string Command { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime FinishedAt { get; set; }
    public bool LiveRequestSent { get; set; }
    public int RequestsSent { get; set; }
    public int? HttpStatus { get; set; }
    public string? ContentType { get; set; }
    public int? BodyLength { get; set; }
    public string? FinalUrlHost { get; set; }
    public IReadOnlyList<RedirectHop>? Redirects { get; set; }
    public string? Classification { get; set; }
    public string? ClassificationReason { get; set; }
    public int? VisibleTextLength { get; set; }
    public PolicySummary? Policy { get; set; }
    public JobPosting? Posting { get; set; }
    public string? Outcome { get; set; }
    public string? Note { get; set; }
}
