using System.Net.Http.Json;
using System.Text.Json;
using JobFetchProbe.Models.Chat;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Ollama;

/// <summary>
/// Typed client for a local Ollama server. The HttpClient is created by IHttpClientFactory (see DependencyInjection):
/// do not dispose it and do not create one here. One attempt. A refusal, a timeout, or an unreadable answer becomes
/// <see cref="LocalChatUnavailableException"/>; the caller's own cancellation is left as it is.
/// </summary>
public sealed class OllamaChatClient(HttpClient http, IOptions<OllamaSettings> settings, ILogger<OllamaChatClient> logger) : ILocalChatModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ChatResponse> CompleteAsync(string message, CancellationToken ct = default)
    {
        return SendAsync(message, format: null, ct);
    }

    public Task<ChatResponse> CompleteJsonAsync(string message, string jsonSchema, CancellationToken ct = default)
    {
        JsonElement format;
        try
        {
            using var schema = JsonDocument.Parse(jsonSchema);
            format = schema.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The JSON Schema supplied to Ollama is invalid.", ex);
        }

        return SendAsync(message, format, ct);
    }

    private async Task<ChatResponse> SendAsync(string message, JsonElement? format, CancellationToken ct)
    {
        var configured = settings.Value;
        var body = new OllamaChatRequest(
            configured.Model,
            Stream: false,
            [new OllamaMessage("user", message)],
            new OllamaOptions(Temperature: 0, configured.ContextTokens, configured.MaxOutputTokens),
            $"{configured.KeepAliveMinutes}m",
            format);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/chat", body, Json, ct);
        }
        catch (HttpRequestException ex)
        {
            throw Unavailable(
                $"Ollama is not reachable at {Address()}. Start Ollama, and check that {configured.Model} is pulled.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw Unavailable($"Ollama did not answer within {configured.TimeoutSeconds} seconds.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw Unavailable($"Ollama answered HTTP {(int)response.StatusCode}.");
            }

            OllamaChatResponse? chat;
            try
            {
                chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(Json, ct);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw Unavailable("Ollama returned an answer this app could not read.", ex);
            }

            var content = chat?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw Unavailable("Ollama returned an empty answer.");
            }

            var model = configured.Model;
            if (!string.IsNullOrWhiteSpace(chat?.Model))
            {
                model = chat.Model.Trim();
            }
            logger.LogInformation(
                "Ollama {Model}: load {Load}s, read {PromptTokens} tokens in {Read}s, wrote {AnswerTokens} tokens in {Write}s\n{Answer}",
                model,
                Seconds(chat?.LoadDuration),
                chat?.PromptEvalCount,
                Seconds(chat?.PromptEvalDuration),
                chat?.EvalCount,
                Seconds(chat?.EvalDuration),
                content.Trim());
            return new ChatResponse(content.Trim(), model);
        }
    }

    private string Address()
    {
        if (http.BaseAddress is null)
        {
            return settings.Value.BaseUrl;
        }
        return http.BaseAddress.GetLeftPart(UriPartial.Authority);
    }

    private static string Seconds(long? nanoseconds)
    {
        if (nanoseconds is null)
        {
            return "?";
        }
        return (nanoseconds.Value / 1_000_000_000d).ToString("0.0");
    }

    private static LocalChatUnavailableException Unavailable(string message, Exception? inner = null)
    {
        return new LocalChatUnavailableException(message, inner);
    }
}
