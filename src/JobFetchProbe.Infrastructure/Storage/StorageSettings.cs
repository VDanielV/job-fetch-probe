namespace JobFetchProbe.Infrastructure.Storage;

/// <summary>Bound from the "Storage" section of appsettings.json.</summary>
public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    public const string JsonProvider = "Json";
    public const string SqliteProvider = "Sqlite";

    /// <summary>
    /// Where jobs, listings, searches and job descriptions are kept: "Json" (files in the Output, Search, Inbox and
    /// Details folders, the original behaviour) or "Sqlite" (one database file, at Storage:Sqlite:Path).
    /// Text rather than an enum, so a typo is reported as a configuration error instead of a crash.
    /// </summary>
    public string Provider { get; set; } = JsonProvider;

    public bool UsesSqlite => string.Equals(Provider?.Trim(), SqliteProvider, StringComparison.OrdinalIgnoreCase);

    public bool IsValidProvider => UsesSqlite || string.Equals(Provider?.Trim(), JsonProvider, StringComparison.OrdinalIgnoreCase);

    public SqliteSettings Sqlite { get; set; } = new();
}

/// <summary>Bound from "Storage:Sqlite".</summary>
public sealed class SqliteSettings
{
    /// <summary>The database file. Created, with its folder, on first use. A relative path is resolved from the folder you run the app in.</summary>
    public string Path { get; set; } = "data/jobs.db";

    public string FullPath => System.IO.Path.GetFullPath(Path);
}
