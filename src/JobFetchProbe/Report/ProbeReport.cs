using JobFetchProbe.Extract;
using JobFetchProbe.Fetch;

namespace JobFetchProbe.Report;

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
    public int ExitCode { get; set; }
    public string? Note { get; set; }
}

public sealed class PolicySummary
{
    public bool GeneralClientDisallowsAll { get; init; }
    public bool JobPathsDisallowedForGeneralClient { get; init; }

    /// <summary>Verbatim rules of the <c>User-agent: *</c> group. Present on policy runs.</summary>
    public IReadOnlyList<string>? GeneralClientRules { get; init; }

    /// <summary>Verbatim rule lines from any group whose path contains "job". Present on policy runs.</summary>
    public IReadOnlyList<string>? JobRuleLines { get; init; }

    /// <summary>On fetch runs: which earlier policy run these booleans came from.</summary>
    public string? Source { get; init; }
}
