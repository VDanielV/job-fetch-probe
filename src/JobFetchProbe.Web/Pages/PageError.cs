namespace JobFetchProbe.Web.Pages;

/// <summary>
/// What the red error pane shows: the kind of error (configuration, database, files or other), a sentence on what it
/// usually means, and the error's own message and type.
/// </summary>
public sealed record PageError(string Title, string Explanation, string Message, string? ErrorType)
{
    /// <summary>The kind of error, worked out from its type (for example Microsoft.Data.Sqlite.SqliteException).</summary>
    public static PageError From(string? errorType, string? message)
    {
        var text = message ?? "No message was recorded.";
        var type = errorType ?? string.Empty;
        if (type.Contains("OptionsValidation", StringComparison.Ordinal))
        {
            return new PageError("Configuration error",
                "A setting in the API's appsettings.json is invalid. Correct it and restart the API.", text, errorType);
        }
        if (type.Contains("Sqlite", StringComparison.Ordinal) || type.Contains("EntityFrameworkCore", StringComparison.Ordinal))
        {
            return new PageError("Database error",
                "The database could not be used: the file may be locked by another program, read-only, damaged or not a database.", text, errorType);
        }
        if (type.StartsWith("System.IO.", StringComparison.Ordinal) || type == "System.UnauthorizedAccessException")
        {
            return new PageError("File or folder error",
                "A folder or file could not be read or written: check the input and archive folders and their permissions.", text, errorType);
        }
        return new PageError("Unexpected error",
            "Something went wrong that the app does not expect. The API's window has the details.", text, errorType);
    }

    /// <summary>A run that showed no sign of life: the process ended without saying how.</summary>
    public static PageError Abandoned(string? message)
    {
        return new PageError("The process ended without finishing",
            "The API (or the console app) was closed, killed or crashed during the acquisition. What was read before is kept.",
            message ?? "No sign of life for 2 minutes.", null);
    }

    /// <summary>The API is not running, so it cannot say why: a bad setting stops it at start-up.</summary>
    public static PageError Unreachable(string message)
    {
        return new PageError("The API is not reachable",
            "Start JobFetchProbe.Api. If it stops at once, its window says why: a configuration error (exit code 2) names the setting to correct.",
            message, null);
    }
}
