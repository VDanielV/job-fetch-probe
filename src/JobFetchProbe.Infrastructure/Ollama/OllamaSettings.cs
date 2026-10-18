namespace JobFetchProbe.Infrastructure.Ollama;

/// <summary>Bound from the "Ollama" section of appsettings.json: where the local model runs.</summary>
public sealed class OllamaSettings
{
    public const string SectionName = "Ollama";

    /// <summary>Ollama's address, e.g. http://localhost:11434.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>The model name Ollama already has, e.g. qwen2.5:3b.</summary>
    public string Model { get; set; } = "qwen2.5:3b";

    /// <summary>How long one answer may take. A small model on CPU often needs most of a minute.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
