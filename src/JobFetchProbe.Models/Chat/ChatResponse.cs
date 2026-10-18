namespace JobFetchProbe.Models.Chat;

/// <summary>The local model's answer to one <see cref="ChatRequest"/>.</summary>
/// <param name="Content">The answer text.</param>
/// <param name="Model">The model that answered, e.g. qwen2.5:3b.</param>
public sealed record ChatResponse(string Content, string Model);
