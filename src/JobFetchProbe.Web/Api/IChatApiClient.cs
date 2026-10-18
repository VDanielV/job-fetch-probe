using JobFetchProbe.Models.Chat;

namespace JobFetchProbe.Web.Api;

/// <summary>
/// The call the web app makes to the API's Chat controller. Throws <see cref="ApiUnavailableException"/> when the API
/// is not reachable or Ollama cannot answer (503), and <see cref="ApiErrorException"/> when the API refuses the message.
/// </summary>
public interface IChatApiClient
{
    /// <summary>POST api/Chat: one message, one answer. Nothing is stored.</summary>
    Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken ct = default);
}
