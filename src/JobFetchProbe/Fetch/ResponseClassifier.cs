using System.Net;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Fetch;

public sealed record ClassifierInput(int Status, Uri? FinalUrl, string VisibleText, bool HasH1, bool TransportFailed);

public sealed record ClassifierResult(FetchClassification Class, string Reason);

/// <summary>
/// Assigns exactly one class, first match wins, in the order of the spec table.
/// Its output is an observation; nothing here suggests or triggers another request.
/// </summary>
public static class ResponseClassifier
{
    public const int MinVisibleText = 400;

    public static readonly IReadOnlyList<int> DeniedStatuses = [401, 403, 429, 999];
    public static readonly IReadOnlyList<string> AuthPathMarkers = ["/login", "/uas/login", "/authwall", "/checkpoint"];
    public static readonly IReadOnlyList<string> ChallengeMarkers = ["security verification", "captcha"];

    public static bool IsAuthPath(Uri url) =>
        AuthPathMarkers.Any(m => url.AbsolutePath.Contains(m, StringComparison.OrdinalIgnoreCase));

    public static ClassifierResult Classify(ClassifierInput input)
    {
        if (input.TransportFailed || input.Status == 0)
        {
            return new(FetchClassification.Error, "transport failure, timeout, or status 0");
        }

        if (DeniedStatuses.Contains(input.Status))
        {
            return new(FetchClassification.Denied, $"status {input.Status}");
        }

        if (input.FinalUrl is not null && IsAuthPath(input.FinalUrl))
        {
            return new(FetchClassification.AuthWall, "final URL is a login, authwall or checkpoint path");
        }

        if (input.Status == 200)
        {
            var marker = ChallengeMarkers.FirstOrDefault(m =>
                input.VisibleText.Contains(m, StringComparison.OrdinalIgnoreCase));
            if (marker is not null)
            {
                return new(FetchClassification.Challenge, $"visible text contains '{marker}'");
            }

            if (input.VisibleText.Length < MinVisibleText)
            {
                return new(FetchClassification.EmptyShell,
                    $"visible text is {input.VisibleText.Length} chars (< {MinVisibleText})");
            }

            if (input.HasH1)
            {
                return new(FetchClassification.DescriptionPresent,
                    $"visible text is {input.VisibleText.Length} chars and an h1 exists");
            }

            // Not in the table explicitly: 200, enough text, no h1. Falls through to the catch-all row.
            return new(FetchClassification.Error,
                $"status 200 with {input.VisibleText.Length} chars of visible text but no h1");
        }

        return new(FetchClassification.Error, $"status {input.Status}");
    }
}

/// <summary>Visible text: script, style and comments removed, tags removed, entities decoded, whitespace collapsed.</summary>
public static partial class HtmlText
{
    public static string Visible(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = NonVisibleBlocks().Replace(html, " ");
        text = Tags().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        return Whitespace().Replace(text, " ").Trim();
    }

    public static bool HasH1(string html) =>
        !string.IsNullOrEmpty(html) && H1().IsMatch(NonVisibleBlocks().Replace(html, " "));

    [GeneratedRegex(@"<script\b.*?</script\s*>|<style\b.*?</style\s*>|<!--.*?-->",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NonVisibleBlocks();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<h1[\s>/]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex H1();
}
