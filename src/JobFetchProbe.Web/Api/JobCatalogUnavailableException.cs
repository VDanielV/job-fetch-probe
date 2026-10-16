namespace JobFetchProbe.Web.Api;

/// <summary>The jobs cannot be shown: the API says there is no database yet (503), or the API is not reachable.</summary>
public sealed class JobCatalogUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
