using System.Text.Json.Serialization;

namespace JobFetchProbe.Infrastructure.Ollama;

/// <summary>
/// The body of POST api/chat. One user message, no history, and a single JSON object back (stream is false).
/// <paramref name="Format"/> is "json" for a fit score, and omitted for ordinary chat.
/// </summary>
internal sealed record OllamaChatRequest(
    string Model,
    bool Stream,
    IReadOnlyList<OllamaMessage> Messages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Format = null);

internal sealed record OllamaMessage(string Role, string Content);

/// <summary>The part of Ollama's answer this app reads. The rest of the payload (timings, token counts) is ignored.</summary>
internal sealed record OllamaChatResponse(string? Model, OllamaReply? Message);

internal sealed record OllamaReply(string? Content);
