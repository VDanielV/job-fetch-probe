using System.Text.Json;
using JobFetchProbe.Models.Fit;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Abstractions.Fit;
using JobFetchProbe.Services.Catalog;
using JobFetchProbe.Services.Chat;
using Microsoft.Extensions.Logging;

namespace JobFetchProbe.Services.Fit;

/// <summary>
/// Loads one stored job, asks the local model for a JSON score, checks the answer, and keeps it
/// (<see cref="IJobFitAssessmentStore"/>) with the model's name and the version of the Fit files.
/// </summary>
public sealed class JobFitService(
    IJobRetriever jobs, IFitDocuments documents, ILocalChatModel model, IJobFitAssessmentStore assessments, TimeProvider time,
    ILogger<JobFitService> logger) : IJobFitService
{
    public async Task<JobFitResult?> AssessAsync(JobFitRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            throw new JobFitRequestException("jobId", "Choose a job to score.");
        }

        var job = await jobs.GetJobAsync(request.JobId.Trim(), ct);
        if (job is null)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(job.Description))
        {
            throw new JobFitRequestException("description", "This job has no description yet, so it cannot be scored.");
        }

        var pack = documents.Load();
        var message = JobFitPrompt.Build(pack, job);
        var answer = await model.CompleteJsonAsync(message, ct);
        var result = Read(answer.Content) with { AssessedAt = time.GetLocalNow() };
        await KeepAsync(job.JobId, result, answer.Model, ProfileVersion.Of(pack), ct);
        return result;
    }

    public async Task<JobFitHistory?> GetHistoryAsync(string jobId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }
        var job = await jobs.GetJobAsync(jobId.Trim(), ct);
        if (job is null)
        {
            return null;
        }

        string? current = null;
        try
        {
            current = ProfileVersion.Of(documents.Load());
        }
        catch (InvalidOperationException)
        {
            // A Fit file is missing or empty: the history is still shown, without marking older profiles.
        }
        return new JobFitHistory(await assessments.GetAsync(job.JobId, ct), current);
    }

    /// <summary>
    /// Stores the score. A failure to store is logged and the score is still returned: scoring is never lost because of
    /// the store. A cancelled request stores nothing.
    /// </summary>
    private async Task KeepAsync(string jobId, JobFitResult result, string? modelName, string profileVersion, CancellationToken ct)
    {
        var assessment = new JobFitAssessment(
            0, result.AssessedAt!.Value, result.Fit, result.Decision, result.HardSkip, result.Reason, result.MissingInformation,
            modelName, profileVersion);
        try
        {
            await assessments.AddAsync(jobId, assessment, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The fit of job {JobId} could not be stored; it was still returned", jobId);
        }
    }

    private static JobFitResult Read(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(StripFence(content));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The answer was not a JSON object.");
            }

            if (!Enum.TryParse<JobFitDecision>(RequiredString(root, "decision"), ignoreCase: true, out var decision))
            {
                throw new JsonException("The decision was not Apply, Maybe, or Skip.");
            }

            var fit = ReadFit(root);
            var reason = RequiredString(root, "reason");
            var hardSkip = ReadBool(root, "hardSkip");
            if (hardSkip)
            {
                decision = JobFitDecision.Skip;
            }

            return new JobFitResult(decision, fit, reason, hardSkip, ReadMissing(root));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            throw new LocalChatUnavailableException("Ollama returned a fit this app could not read.", ex);
        }
    }

    private static string StripFence(string content)
    {
        var text = content.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLine = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        if (firstLine < 0 || lastFence <= firstLine)
        {
            return text;
        }

        return text[(firstLine + 1)..lastFence].Trim();
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!TryProperty(root, name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"The answer had no {name}.");
        }

        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new JsonException($"The answer had an empty {name}.");
        }

        return text;
    }

    private static int ReadFit(JsonElement root)
    {
        if (!TryProperty(root, "fit", out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("The answer had no fit number.");
        }

        var fit = (int)Math.Round(value.GetDouble(), MidpointRounding.AwayFromZero);
        if (fit is < 0 or > 100)
        {
            throw new JsonException("The fit was outside 0 to 100.");
        }

        return fit;
    }

    private static bool ReadBool(JsonElement root, string name)
    {
        if (!TryProperty(root, name, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonException($"The answer had no {name}.");
        }

        return value.GetBoolean();
    }

    private static IReadOnlyList<string> ReadMissing(JsonElement root)
    {
        if (!TryProperty(root, "missingInformation", out var value))
        {
            return [];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("missingInformation was not a list.");
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("missingInformation contained something other than text.");
            }

            var text = item.GetString()?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                items.Add(text);
            }
        }

        return items;
    }

    private static bool TryProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
