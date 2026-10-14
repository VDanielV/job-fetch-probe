namespace JobFetchProbe.Domain.Rules;

/// <summary>Rules for the single operator-supplied job URL (spec, Phase 2).</summary>
public static class JobUrlRules
{
    public const string AllowedHost = "www.linkedin.com";
    public const string JobViewPrefix = "/jobs/view/";

    /// <summary>
    /// Validates the URL and returns it without query string or fragment,
    /// so tracking parameters are never sent.
    /// </summary>
    /// <exception cref="ArgumentException">The URL breaks one of the rules. The message says which.</exception>
    public static Uri Normalize(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var u))
        {
            throw new ArgumentException("--url must be an absolute URL");
        }
        if (u.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("--url must use https");
        }
        if (!string.IsNullOrEmpty(u.UserInfo))
        {
            throw new ArgumentException("--url must not contain credentials");
        }
        if (!u.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"--url host must be {AllowedHost}");
        }
        if (!u.IsDefaultPort)
        {
            throw new ArgumentException("--url must use the default https port");
        }

        var path = u.AbsolutePath;
        if (path.Contains("/search", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/jobs-guest", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("search pages and guest endpoints are out of scope");
        }
        if (u.Query.Contains("start=", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("pagination parameters are out of scope");
        }

        foreach (var pair in u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var key = Uri.UnescapeDataString(pair.Split('=', 2)[0]);
            if (key.Equals("currentJobId", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("currentJobId walks a result list; pass the single job view URL instead");
            }
        }

        if (!path.StartsWith(JobViewPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"--url path must be a single job view ({JobViewPrefix}...)");
        }

        return new UriBuilder(u) { Query = string.Empty, Fragment = string.Empty }.Uri;
    }
}
