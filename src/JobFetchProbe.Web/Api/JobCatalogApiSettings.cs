namespace JobFetchProbe.Web.Api;

/// <summary>Bound from the "JobCatalogApi" section of appsettings.json: where the API runs.</summary>
public sealed class JobCatalogApiSettings
{
    public const string SectionName = "JobCatalogApi";

    /// <summary>The API's address, e.g. http://localhost:5081 (JobFetchProbe.Api's launch profile).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5081";

    /// <summary>How long one call may take before the page reports the API as not reachable.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
