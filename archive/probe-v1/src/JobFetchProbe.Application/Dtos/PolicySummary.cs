namespace JobFetchProbe.Application.Dtos;

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
