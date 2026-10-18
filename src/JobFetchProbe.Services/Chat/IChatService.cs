using JobFetchProbe.Models.Chat;

namespace JobFetchProbe.Services.Chat;

/// <summary>
/// One turn of chat. Checks the message, then asks the local model. Nothing is stored.
/// Throws <see cref="ChatRequestException"/> when the message is blank or too long.
/// </summary>
public interface IChatService
{
    Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken ct = default);
}
