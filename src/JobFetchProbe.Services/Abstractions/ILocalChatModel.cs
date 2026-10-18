using JobFetchProbe.Models.Chat;

namespace JobFetchProbe.Services.Abstractions;

/// <summary>
/// One turn with the local model. Implemented by the infrastructure layer, which knows how to reach it.
/// The service layer passes plain text and gets <see cref="ChatResponse"/> back.
/// </summary>
public interface ILocalChatModel
{
    /// <summary>
    /// Sends one message and returns the answer. <paramref name="message"/> is already checked.
    /// Throws <see cref="Chat.LocalChatUnavailableException"/> when the model cannot answer.
    /// </summary>
    Task<ChatResponse> CompleteAsync(string message, CancellationToken ct = default);

    /// <summary>Same as <see cref="CompleteAsync"/>, and the model must answer with a JSON object.</summary>
    Task<ChatResponse> CompleteJsonAsync(string message, CancellationToken ct = default);
}
