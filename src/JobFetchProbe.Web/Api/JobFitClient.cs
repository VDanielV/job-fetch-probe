using System.Net;
using JobFetchProbe.Models.Fit;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// Typed HTTP client for the API's JobFit controller. Same API address as the catalog. Its own time-out is
/// <see cref="TimeoutSeconds"/>, a little longer than the API's 120 seconds for Ollama, so the API's own answer arrives.
/// </summary>
public sealed class JobFitClient(HttpClient http) : IJobFitClient
{
    public const int TimeoutSeconds = 130;

    private const string Route = "api/JobFit";

    public async Task<JobFitResult> AssessAsync(string jobId, CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.PostAsJsonAsync(Route, new JobFitRequest(jobId), ApiJson.Options, ct), ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ApiErrorException(await ReadValidationMessageAsync(response, ct), errorType: null);
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ApiErrorException($"Job {jobId} is not in the database any more.", errorType: null);
        }
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var problem = await ApiCall.ReadProblemAsync(response, ct);
            var message = "Ollama is not available.";
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                message = problem.Detail;
            }
            throw new ApiUnavailableException(message);
        }

        await ApiCall.EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<JobFitResult>(ApiJson.Options, ct);
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
        return "The API refused to score this job.";
    }
}
