namespace JobFetchProbe.Policy;

/// <summary>
/// Parses robots.txt into groups and answers the two policy questions in the spec.
/// Pure function of the body, so tests never touch the network.
/// </summary>
public static class RobotsPolicyParser
{
    /// <summary>Paths checked by literal prefix matching of <c>Disallow</c> values.</summary>
    public static readonly IReadOnlyList<string> JobPaths = ["/jobs", "/jobs-guest/", "/job-apply/"];

    /// <summary>
    /// A group starts at <c>User-agent</c> and owns the following Allow/Disallow lines until the next
    /// <c>User-agent</c>. Consecutive <c>User-agent</c> lines (no rule between them) share one group,
    /// as in the robots.txt standard (RFC 9309).
    /// </summary>
    public static IReadOnlyList<RobotsGroup> ParseGroups(string body)
    {
        var groups = new List<RobotsGroup>();
        List<string>? agents = null;
        List<RobotsRule>? rules = null;
        var lastWasAgent = false;

        foreach (var raw in body.Split('\n'))
        {
            var line = raw;
            var hash = line.IndexOf('#');
            if (hash >= 0)
            {
                line = line[..hash];
            }
            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var field = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();

            if (field.Equals("user-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (!lastWasAgent)
                {
                    agents = [];
                    rules = [];
                    groups.Add(new RobotsGroup(agents, rules));
                }
                agents!.Add(value);
                lastWasAgent = true;
            }
            else if (field.Equals("allow", StringComparison.OrdinalIgnoreCase) ||
                     field.Equals("disallow", StringComparison.OrdinalIgnoreCase))
            {
                lastWasAgent = false;
                // Rules before the first User-agent belong to no group and are ignored.
                rules?.Add(new RobotsRule(
                    field.Equals("allow", StringComparison.OrdinalIgnoreCase) ? "Allow" : "Disallow",
                    value));
            }
            else
            {
                // Sitemap, Crawl-delay and other fields end a run of User-agent lines.
                lastWasAgent = false;
            }
        }

        return groups;
    }

    public static RobotsPolicyReport Analyze(string body, int httpStatus)
    {
        var groups = ParseGroups(body);

        var generalRules = groups
            .Where(g => g.UserAgents.Any(a => a == "*"))
            .SelectMany(g => g.Rules)
            .ToList();

        var disallowsAll = generalRules.Any(r => r.Directive == "Disallow" && r.Value == "/");

        var jobPathsDisallowed = disallowsAll || generalRules.Any(r =>
            r.Directive == "Disallow" &&
            r.Value.Length > 0 &&
            JobPaths.Any(p => p.StartsWith(r.Value, StringComparison.Ordinal)));

        var jobLines = groups
            .SelectMany(g => g.Rules
                .Where(r => r.Value.Contains("job", StringComparison.OrdinalIgnoreCase))
                .Select(r => $"[{string.Join(", ", g.UserAgents)}] {r}"))
            .ToList();

        return new RobotsPolicyReport(
            httpStatus,
            body.Length,
            generalRules.Select(r => r.ToString()).ToList(),
            disallowsAll,
            jobLines,
            jobPathsDisallowed);
    }
}
