namespace JobFetchProbe.Domain.Models;

/// <summary>What robots.txt says about a general client and about job paths. Rule lines are verbatim.</summary>
public sealed record RobotsPolicy(
    IReadOnlyList<string> GeneralClientRules,
    bool GeneralClientDisallowsAll,
    IReadOnlyList<string> JobRuleLines,
    bool JobPathsDisallowedForGeneralClient);
