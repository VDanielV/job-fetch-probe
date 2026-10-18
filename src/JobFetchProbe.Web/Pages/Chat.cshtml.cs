using JobFetchProbe.Models.Chat;
using JobFetchProbe.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobFetchProbe.Web.Pages;

/// <summary>
/// One message to the local model (POST api/Chat). The message and the answer exist only on this response.
/// Nothing is written to the database, TempData, session, or the browser.
/// </summary>
public sealed class ChatModel(IChatApiClient chat) : PageModel
{
    [BindProperty]
    public string? Message { get; set; }

    /// <summary>The model's answer, when the call succeeded.</summary>
    public string? Answer { get; private set; }

    /// <summary>The model that wrote <see cref="Answer"/>, e.g. qwen2.5:3b.</summary>
    public string? ModelName { get; private set; }

    /// <summary>Why there is no answer: a refused message, or the API or Ollama is not available.</summary>
    public string? Error { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            Error = "Write a message.";
            return Page();
        }

        var text = Message.Trim();
        if (text.Length > ChatRequest.MaxMessageLength)
        {
            Error = $"The message can be at most {ChatRequest.MaxMessageLength} characters.";
            return Page();
        }

        try
        {
            var response = await chat.SendAsync(new ChatRequest(text), ct);
            Message = text;
            Answer = response.Content;
            ModelName = response.Model;
        }
        catch (Exception ex) when (ex is ApiUnavailableException or ApiErrorException)
        {
            Error = ex.Message;
        }

        return Page();
    }
}
