using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// Sends one request to the API; a connection failure or a time-out becomes <see cref="ApiUnavailableException"/>,
/// and an error answer becomes <see cref="ApiErrorException"/> with the API's message.
/// </summary>
internal static class ApiCall
{
    /// <summary>Throws <see cref="ApiErrorException"/> unless the answer is a success, with the problem details the API sent.</summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await ReadProblemAsync(response, ct);
        string? errorType = null;
        if (problem is not null && problem.Extensions.TryGetValue("errorType", out var value) && value is not null)
        {
            errorType = value.ToString();
        }

        var message = problem?.Detail;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"The API answered {(int)response.StatusCode} {response.ReasonPhrase}.";
        }
        throw new ApiErrorException(message, errorType);
    }

    /// <summary>The problem details in an error answer, or null when the answer has none.</summary>
    public static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null; // not JSON at all
        }
    }

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
