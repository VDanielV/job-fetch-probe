namespace JobFetchProbe;

public enum ProbeCommand
{
    Policy,
    Extract,
    Fetch,
}

/// <summary>
/// Parsed command line. Parsing throws <see cref="ArgumentException"/> for anything invalid;
/// the runner maps that to exit code 2 before any network object exists.
/// </summary>
public sealed record ProbeOptions
{
    public const string Usage =
        """
        usage:
          job-fetch-probe policy
          job-fetch-probe extract --html <path> [--extractor fixture]
          job-fetch-probe fetch --url <https://www.linkedin.com/jobs/view/...> --live

        shared flags:
          --out <dir>               report directory (default: out)
          --timeout-seconds <n>     5 to 30 (default: 15)
        """;

    public required ProbeCommand Command { get; init; }
    public string? HtmlPath { get; init; }
    public Uri? Url { get; init; }
    public bool Live { get; init; }
    public string Extractor { get; init; } = "fixture";
    public string OutDir { get; init; } = "out";
    public int TimeoutSeconds { get; init; } = 15;

    public static ProbeOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new ArgumentException("a command is required: policy, extract or fetch");
        }

        var command = args[0].ToLowerInvariant() switch
        {
            "policy" => ProbeCommand.Policy,
            "extract" => ProbeCommand.Extract,
            "fetch" => ProbeCommand.Fetch,
            _ => throw new ArgumentException($"unknown command '{args[0]}'"),
        };

        string? html = null;
        string? rawUrl = null;
        var live = false;
        var extractor = "fixture";
        var outDir = "out";
        var timeout = 15;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < args.Count; i++)
        {
            var flag = args[i];
            if (!seen.Add(flag))
            {
                throw new ArgumentException($"{flag} given more than once (one value per run)");
            }

            switch (flag)
            {
                case "--out":
                    outDir = Value(args, ref i, flag);
                    break;
                case "--timeout-seconds":
                    var t = Value(args, ref i, flag);
                    if (!int.TryParse(t, out timeout) || timeout < 5 || timeout > 30)
                    {
                        throw new ArgumentException("--timeout-seconds must be an integer from 5 to 30");
                    }
                    break;
                case "--html" when command == ProbeCommand.Extract:
                    html = Value(args, ref i, flag);
                    break;
                case "--extractor" when command == ProbeCommand.Extract:
                    extractor = Value(args, ref i, flag).ToLowerInvariant();
                    break;
                case "--url" when command == ProbeCommand.Fetch:
                    rawUrl = Value(args, ref i, flag);
                    break;
                case "--live" when command == ProbeCommand.Fetch:
                    live = true;
                    break;
                default:
                    throw new ArgumentException($"'{flag}' is not a valid argument for {command.ToString().ToLowerInvariant()}");
            }
        }

        Uri? url = null;
        switch (command)
        {
            case ProbeCommand.Extract:
                if (string.IsNullOrWhiteSpace(html))
                {
                    throw new ArgumentException("extract needs --html <path>");
                }
                if (!File.Exists(html))
                {
                    throw new ArgumentException($"html file not found: {Path.GetFileName(html)}");
                }
                if (extractor == "saved")
                {
                    throw new ArgumentException("saved extractor is not part of this build");
                }
                if (extractor != "fixture")
                {
                    throw new ArgumentException($"unknown extractor '{extractor}'");
                }
                break;

            case ProbeCommand.Fetch:
                if (string.IsNullOrWhiteSpace(rawUrl))
                {
                    throw new ArgumentException("fetch needs --url <https-url>");
                }
                url = JobUrlRules.Normalize(rawUrl);
                if (!live)
                {
                    throw new ArgumentException("pass --live to send the single request");
                }
                break;
        }

        return new ProbeOptions
        {
            Command = command,
            HtmlPath = html,
            Url = url,
            Live = live,
            Extractor = extractor,
            OutDir = outDir,
            TimeoutSeconds = timeout,
        };
    }

    private static string Value(IReadOnlyList<string> args, ref int i, string flag)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{flag} needs a value");
        }
        return args[++i];
    }
}

/// <summary>
/// Rules for the single operator-supplied job URL (spec, Phase 2).
/// </summary>
public static class JobUrlRules
{
    public const string AllowedHost = "www.linkedin.com";

    /// <summary>
    /// Validates the URL and returns it without query string or fragment,
    /// so tracking parameters are never sent.
    /// </summary>
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

        if (!path.StartsWith("/jobs/view/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--url path must be a single job view (/jobs/view/...)");
        }

        return new UriBuilder(u) { Query = string.Empty, Fragment = string.Empty }.Uri;
    }
}
