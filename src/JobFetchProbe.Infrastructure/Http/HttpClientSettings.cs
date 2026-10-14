namespace JobFetchProbe.Infrastructure.Http;

/// <summary>Bound from the "HttpClient" section of appsettings.json.</summary>
public sealed class HttpClientSettings
{
    public const string SectionName = "HttpClient";

    public int TimeoutSeconds { get; set; } = 20;

    public string UserAgent { get; set; } = "JobFetchProbe/0.3 (personal study project)";

    /// <summary>How long the factory reuses one connection handler before rotating it (picks up DNS changes).</summary>
    public int HandlerLifetimeMinutes { get; set; } = 5;
}
