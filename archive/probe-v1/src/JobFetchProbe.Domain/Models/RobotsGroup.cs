namespace JobFetchProbe.Domain.Models;

/// <summary>A robots.txt group: one or more <c>User-agent</c> lines and the rules that follow them.</summary>
public sealed record RobotsGroup(IReadOnlyList<string> UserAgents, IReadOnlyList<RobotsRule> Rules);
