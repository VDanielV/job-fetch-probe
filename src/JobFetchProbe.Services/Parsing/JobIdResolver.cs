using System.Net;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Services.Parsing;

/// <summary>
/// Finds the job ID: the number at the end of a /jobs/view/ path, e.g.
/// /jobs/view/4478111194/ or /jobs/view/tecnico-a-de-desarrollo-at-company-4478111194.
/// </summary>
public static partial class JobIdResolver
{
    public static string? FromUrl(Uri url)
    {
        var m = IdInPath().Match(url.AbsolutePath);
        if (!m.Success)
        {
            return null;
        }
        return m.Groups[1].Value;
    }

    /// <summary>From the page's canonical link or og:url (works for saved pages).</summary>
    public static string? FromHtml(string html)
    {
        if (CanonicalUrl(html) is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return FromUrl(uri);
        }
        return null;
    }

    /// <summary>The page's own address from its canonical link or og:url, if it is an absolute http(s) URL.</summary>
    public static Uri? CanonicalUri(string html)
    {
        return UrlHelper.AbsoluteWebUri(CanonicalUrl(html));
    }

    public static string? CanonicalUrl(string html)
    {
        var m = Canonical().Match(html);
        if (!m.Success)
        {
            m = OgUrl().Match(html);
        }
        if (!m.Success)
        {
            return null;
        }
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    [GeneratedRegex(@"/jobs/view/(?:[^/]*?-)?(\d{6,})/?$", RegexOptions.IgnoreCase)]
    private static partial Regex IdInPath();

    [GeneratedRegex(@"<link\s+rel=""canonical""\s+href=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex Canonical();

    [GeneratedRegex(@"<meta\s+property=""og:url""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgUrl();
}
