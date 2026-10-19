using System.Net;
using System.Text.Json;
using JobFetchProbe.Models.Applications;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Web.Api;

/// <summary>Typed HTTP client for the API's Applications controller (base address and time-out as for the catalog).</summary>
public sealed class ApplicationClient(HttpClient http) : IApplicationClient
{
    private const string Route = "api/Applications";

    public async Task<Application> ApplyAsync(string jobId, CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.PostAsJsonAsync(Route, new ApplyRequest(jobId), ApiJson.Options, ct), ct);
        return await ReadApplicationAsync(response, ct);
    }

    public async Task<int> UndoAsync(string jobId, bool withNotes, CancellationToken ct = default)
    {
        var path = $"{Route}/{Uri.EscapeDataString(jobId)}";
        if (withNotes)
        {
            path += "?withNotes=true";
        }
        using var response = await ApiCall.SendAsync(http, () => http.DeleteAsync(path, ct), ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await ApiCall.ReadProblemAsync(response, ct);
            if (problem is not null && problem.Extensions.TryGetValue("notes", out var value) && value is JsonElement { ValueKind: JsonValueKind.Number } number)
            {
                return number.GetInt32();
            }
        }
        await ApiCall.EnsureSuccessAsync(response, ct);
        return 0;
    }

    public async Task<ApplicationPage> GetPageAsync(
        bool closed, ApplicationStage? stage, bool flagged, int page, int pageSize, ApplicationSort sort = ApplicationSort.FollowUp,
        CancellationToken ct = default)
    {
        var path = $"{Route}?page={page}&pageSize={pageSize}";
        if (closed)
        {
            path += "&closed=true";
        }
        if (stage is not null)
        {
            path += "&stage=" + stage.Value;
        }
        if (flagged)
        {
            path += "&flagged=true";
        }
        if (sort == ApplicationSort.Fit)
        {
            path += "&sort=Fit";
        }
        using var response = await ApiCall.SendAsync(http, () => http.GetAsync(path, ct), ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ApplicationPage>(ApiJson.Options, ct)
            ?? throw new InvalidOperationException("The API returned an empty page.");
    }

    public async Task<Application?> GetAsync(string jobId, CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.GetAsync($"{Route}/{Uri.EscapeDataString(jobId)}", ct), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<Application>(ApiJson.Options, ct);
    }

    public async Task<Application> SetStageAsync(string jobId, ApplicationStage stage, CancellationToken ct = default)
    {
        var path = $"{Route}/{Uri.EscapeDataString(jobId)}/stage";
        using var response = await ApiCall.SendAsync(http, () => http.PutAsJsonAsync(path, new StageRequest(stage), ApiJson.Options, ct), ct);
        return await ReadApplicationAsync(response, ct);
    }

    public async Task<Application> AddNoteAsync(string jobId, string text, CancellationToken ct = default)
    {
        var path = $"{Route}/{Uri.EscapeDataString(jobId)}/notes";
        using var response = await ApiCall.SendAsync(http, () => http.PostAsJsonAsync(path, new NoteRequest(text), ApiJson.Options, ct), ct);
        return await ReadApplicationAsync(response, ct);
    }

    public async Task<ApplicationsSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        using var response = await ApiCall.SendAsync(http, () => http.GetAsync(Route + "/summary", ct), ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ApplicationsSummary>(ApiJson.Options, ct)
            ?? throw new InvalidOperationException("The API returned an empty answer.");
    }

    private static async Task<Application> ReadApplicationAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<Application>(ApiJson.Options, ct)
            ?? throw new InvalidOperationException("The API returned an empty answer.");
    }

    /// <summary>
    /// Like <see cref="ApiCall.EnsureSuccessAsync"/>, but a validation problem (400) gives its first message, and a missing
    /// database (503) is reported as the API being unavailable.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ApiErrorException(await ReadValidationMessageAsync(response, ct), errorType: null);
        }
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var problem = await ApiCall.ReadProblemAsync(response, ct);
            var message = "The API has no database yet.";
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                message = problem.Detail;
            }
            throw new ApiUnavailableException(message);
        }
        await ApiCall.EnsureSuccessAsync(response, ct);
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
        catch (JsonException)
        {
            // Not problem details: the generic text below.
        }
        return "The API refused the request.";
    }
}
