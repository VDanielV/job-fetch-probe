namespace JobFetchProbe.Services.Chat;

/// <summary>
/// The local model could not answer: it is not running, it timed out, or its answer could not be read.
/// The message was not stored.
/// </summary>
public sealed class LocalChatUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
