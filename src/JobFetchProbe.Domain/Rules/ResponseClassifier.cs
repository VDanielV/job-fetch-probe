using JobFetchProbe.Domain.Models;

namespace JobFetchProbe.Domain.Rules;

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

    public static ClassificationResult Classify(ClassifierInput input)
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
