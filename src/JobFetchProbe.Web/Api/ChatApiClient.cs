using System.Net;
using JobFetchProbe.Models.Chat;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// Typed HTTP client for the API's Chat controller. Same API address as the catalog. Its own time-out is
/// <see cref="TimeoutSeconds"/>, longer than the catalog's, because one local answer can take most of a minute.
/// </summary>
public sealed class ChatApiClient(HttpClient http) : IChatApiClient
{
    public const int TimeoutSeconds = 130;

    private const string Route = "api/Chat";

    public async Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.PostAsJsonAsync(Route, request, ApiJson.Options, ct), ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ApiErrorException(await ReadValidationMessageAsync(response, ct), errorType: null);
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var problem = await ApiCall.ReadProblemAsync(response, ct);
            throw new ApiUnavailableException(problem?.Detail ?? "Ollama is not available.");
        }

        await ApiCall.EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(ApiJson.Options, ct);
        if (result is null || string.IsNullOrWhiteSpace(result.Content))
        {
            throw new InvalidOperationException("The API returned an empty answer.");
        }
        return result;
    }

    private static async Task<string> ReadValidationMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(ct);
            var first = problem?.Errors.SelectMany(e => e.Value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not problem details: the generic text below.
        }
        return "The API refused the message.";
    }
}
