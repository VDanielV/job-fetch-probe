namespace JobFetchProbe.Web.Api;

/// <summary>
/// The API cannot answer: it is not reachable (not started, wrong address, time-out), or it says it has no database
/// yet (503). The message is ready to show on the page.
/// </summary>
public sealed class ApiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
