using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobFetchProbe.Infrastructure.Ollama;

/// <summary>
/// The body of POST api/chat. One user message, no history, and a single JSON object back (stream is false).
/// <paramref name="Format"/> is a JSON Schema for a fit score, and omitted for ordinary chat.
/// </summary>
internal sealed record OllamaChatRequest(
    string Model,
    bool Stream,
    IReadOnlyList<OllamaMessage> Messages,
    OllamaOptions Options,
    [property: JsonPropertyName("keep_alive")] string KeepAlive,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Format = null);

internal sealed record OllamaMessage(string Role, string Content);

internal sealed record OllamaOptions(
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("num_ctx")] int ContextTokens,
    [property: JsonPropertyName("num_predict")] int MaxOutputTokens);

/// <summary>The part of Ollama's answer this app reads, plus the timings written to the API log.</summary>
internal sealed record OllamaChatResponse(
    string? Model,
    OllamaReply? Message,
    [property: JsonPropertyName("load_duration")] long? LoadDuration,
    [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
    [property: JsonPropertyName("prompt_eval_duration")] long? PromptEvalDuration,
    [property: JsonPropertyName("eval_count")] int? EvalCount,
    [property: JsonPropertyName("eval_duration")] long? EvalDuration);

internal sealed record OllamaReply(string? Content);
