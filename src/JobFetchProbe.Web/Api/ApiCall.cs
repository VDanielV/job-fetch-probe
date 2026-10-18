namespace JobFetchProbe.Web.Api;

/// <summary>Sends one request to the API; a connection failure or a time-out becomes <see cref="ApiUnavailableException"/>.</summary>
internal static class ApiCall
{
    public static async Task<HttpResponseMessage> SendAsync(HttpClient http, Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        try
        {
            return await send();
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new ApiUnavailableException(
                $"The API is not reachable at {http.BaseAddress}. Start JobFetchProbe.Api (or check JobCatalogApi:BaseUrl).", ex);
        }
    }
}
