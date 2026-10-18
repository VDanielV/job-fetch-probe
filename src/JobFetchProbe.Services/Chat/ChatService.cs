using JobFetchProbe.Models.Chat;
using JobFetchProbe.Services.Abstractions;

namespace JobFetchProbe.Services.Chat;

/// <summary>Checks one message, then forwards it to <see cref="ILocalChatModel"/>.</summary>
public sealed class ChatService(ILocalChatModel model) : IChatService
{
    public Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ChatRequestException("Write a message.");
        }

        var message = request.Message.Trim();
        if (message.Length > ChatRequest.MaxMessageLength)
        {
            throw new ChatRequestException($"The message can be at most {ChatRequest.MaxMessageLength} characters.");
        }

        return model.CompleteAsync(message, ct);
    }
}
