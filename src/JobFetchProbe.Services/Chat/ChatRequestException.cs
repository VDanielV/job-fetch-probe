namespace JobFetchProbe.Services.Chat;

/// <summary>A chat message that breaks the rules (blank, or longer than the limit). The model was not called.</summary>
public sealed class ChatRequestException(string message) : Exception(message);
