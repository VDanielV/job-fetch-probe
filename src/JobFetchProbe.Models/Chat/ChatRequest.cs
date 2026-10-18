using System.ComponentModel.DataAnnotations;

namespace JobFetchProbe.Models.Chat;

/// <summary>One message for the local model. Each request stands alone: no history is kept.</summary>
/// <param name="Message">Required, not blank, at most <see cref="MaxMessageLength"/> characters.</param>
public sealed record ChatRequest(
    [Required(ErrorMessage = "Write a message.")]
    [StringLength(ChatRequest.MaxMessageLength, ErrorMessage = "The message can be at most {1} characters.")]
    string Message)
{
    public const int MaxMessageLength = 20_000;
}
