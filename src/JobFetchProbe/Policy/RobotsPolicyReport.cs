namespace JobFetchProbe.Policy;

public sealed record RobotsRule(string Directive, string Value)
{
    public override string ToString() => $"{Directive}: {Value}";
}

public sealed record RobotsGroup(IReadOnlyList<string> UserAgents, IReadOnlyList<RobotsRule> Rules);

/// <summary>What robots.txt says, as stored in the report. Rule lines are copied verbatim from the response.</summary>
public sealed record RobotsPolicyReport(
    int HttpStatus,
    int BodyLength,
    IReadOnlyList<string> GeneralClientRules,
    bool GeneralClientDisallowsAll,
    IReadOnlyList<string> JobRuleLines,
    bool JobPathsDisallowedForGeneralClient);
