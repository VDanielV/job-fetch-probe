using System.Net;

namespace JobFetchProbe.Services.Parsing;

/// <summary>Web addresses found in pages: is it http(s), its query values, a link without its tracking parameters.</summary>
internal static class UrlHelper
{
    /// <summary>True for http and https addresses.</summary>
    public static bool IsWeb(Uri url)
    {
        return url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp;
    }

    /// <summary>The text as an absolute http(s) address, or null.</summary>
    public static Uri? AbsoluteWebUri(string? text)
    {
        if (text is null || !Uri.TryCreate(text, UriKind.Absolute, out var url) || !IsWeb(url))
        {
            return null;
        }
        return url;
    }

    /// <summary>A query value of the address, decoded ("Senior+.NET" → "Senior .NET"); null when missing or empty.</summary>
    public static string? QueryValue(Uri url, string name)
    {
        foreach (var pair in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var value = Uri.UnescapeDataString(parts[1].Replace('+', ' ')).Trim();
                if (value.Length == 0)
                {
                    return null;
                }
                return value;
            }
        }
        return null;
    }

    /// <summary>A link from a page: decoded, resolved against the page, without query and fragment (tracking parameters). Null unless http(s).</summary>
    public static Uri? WithoutQuery(string href, Uri pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, WebUtility.HtmlDecode(href), out var url) || !IsWeb(url))
        {
            return null;
        }
        return new UriBuilder(url) { Query = string.Empty, Fragment = string.Empty }.Uri;
    }
}
