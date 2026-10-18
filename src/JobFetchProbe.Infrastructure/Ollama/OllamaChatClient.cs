using System.Net.Http.Json;
using System.Text.Json;
using JobFetchProbe.Models.Chat;
using JobFetchProbe.Services.Abstractions;
using JobFetchProbe.Services.Chat;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Ollama;

/// <summary>
/// Typed client for a local Ollama server. The HttpClient is created by IHttpClientFactory (see DependencyInjection):
/// do not dispose it and do not create one here. One attempt. A refusal, a timeout, or an unreadable answer becomes
/// <see cref="LocalChatUnavailableException"/>; the caller's own cancellation is left as it is.
/// </summary>
public sealed class OllamaChatClient(HttpClient http, IOptions<OllamaSettings> settings) : ILocalChatModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ChatResponse> CompleteAsync(string message, CancellationToken ct = default)
    {
        return SendAsync(message, format: null, ct);
    }

    public Task<ChatResponse> CompleteJsonAsync(string message, CancellationToken ct = default)
    {
        return SendAsync(message, format: "json", ct);
    }

    private async Task<ChatResponse> SendAsync(string message, string? format, CancellationToken ct)
    {
        var configured = settings.Value;
        var body = new OllamaChatRequest(configured.Model, Stream: false, [new OllamaMessage("user", message)], format);

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

    private static LocalChatUnavailableException Unavailable(string message, Exception? inner = null)
    {
        return new LocalChatUnavailableException(message, inner);
    }
}
