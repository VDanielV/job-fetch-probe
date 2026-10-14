namespace JobFetchProbe.Domain.Models;

/// <summary>One <c>Allow</c> or <c>Disallow</c> line from robots.txt.</summary>
public sealed record RobotsRule(string Directive, string Value)
{
    public override string ToString() => $"{Directive}: {Value}";
}
