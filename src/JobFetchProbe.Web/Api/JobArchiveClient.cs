using System.Net;
using JobFetchProbe.Models.Archive;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.Api;

/// <summary>Typed HTTP client for the API's ArchivedJobs controller (base address and time-out as for the catalog).</summary>
public sealed class JobArchiveClient(HttpClient http) : IJobArchiveClient
{
    private const string Route = "api/ArchivedJobs";

    public async Task<ArchiveResult> ArchiveAsync(ArchiveRequest request, CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.PostAsJsonAsync(Route, request, ApiJson.Options, ct), ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The rules are checked by the API: its validation problem says what is wrong.
            throw new ApiErrorException(await ReadValidationMessageAsync(response, ct), errorType: null);
        }

        await ApiCall.EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<ArchiveResult>(ApiJson.Options, ct);
        if (result is null)
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
        return "The API refused the request.";
    }
}
