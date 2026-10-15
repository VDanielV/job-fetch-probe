using System.Net;
using System.Text.RegularExpressions;

namespace JobFetchProbe.Services.Parsing;

/// <summary>
/// Finds the job ID: the number at the end of a /jobs/view/ path, e.g.
/// /jobs/view/4478111194/ or /jobs/view/tecnico-a-de-desarrollo-at-company-4478111194.
/// </summary>
public static partial class JobIdResolver
{
    public static string? FromUrl(Uri url) => FromPath(url.AbsolutePath);

    /// <summary>From the page's canonical link or og:url (works for saved pages).</summary>
    public static string? FromHtml(string html) =>
        CanonicalUrl(html) is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? FromUrl(uri) : null;

    /// <summary>From a file name such as "view-source_https___www.linkedin.com_jobs_view_4478111194.html".</summary>
    public static string? FromFileName(string path)
    {
        var m = IdInFileName().Match(Path.GetFileNameWithoutExtension(path));
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>The page's own address from its canonical link or og:url, if it is an absolute http(s) URL.</summary>
    public static Uri? CanonicalUri(string html) =>
        CanonicalUrl(html) is { } url &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;

    public static string? CanonicalUrl(string html)
    {
        var m = Canonical().Match(html);
        if (!m.Success)
        {
            m = OgUrl().Match(html);
        }
        return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    private static string? FromPath(string path)
    {
        var m = IdInPath().Match(path);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"/jobs/view/(?:[^/]*?-)?(\d{6,})/?$", RegexOptions.IgnoreCase)]
    private static partial Regex IdInPath();

    [GeneratedRegex(@"jobs_view_(?:.*?-)?(\d{6,})", RegexOptions.IgnoreCase)]
    private static partial Regex IdInFileName();

    [GeneratedRegex(@"<link\s+rel=""canonical""\s+href=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex Canonical();

    [GeneratedRegex(@"<meta\s+property=""og:url""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgUrl();
}
