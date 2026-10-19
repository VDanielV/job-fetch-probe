namespace JobFetchProbe.Web.Api;

/// <summary>Bound from the "JobCatalogApi" section of appsettings.json: where the API runs.</summary>
public sealed class JobCatalogApiSettings
{
    public const string SectionName = "JobCatalogApi";

    /// <summary>The API's address, e.g. http://localhost:5081 (JobFetchProbe.Api's launch profile).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5081";

    /// <summary>How long one call may take before the page reports the API as not reachable.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// How long a call that waits for the local model (Check fit, Chat) may take. Keep it a little longer than the API's
    /// Ollama:TimeoutSeconds, so the API's own answer (a score, or why there is none) arrives first.
    /// </summary>
    public int LocalModelTimeoutSeconds { get; set; } = 250;
}
