namespace JobFetchProbe.Web.Api;

/// <summary>
/// The API answered with an error it could not handle (500): its message, and the type of the error behind it (for
/// example Microsoft.Data.Sqlite.SqliteException), when the API says.
/// </summary>
public sealed class ApiErrorException(string message, string? errorType) : Exception(message)
{
    public string? ErrorType { get; } = errorType;
}
