namespace JobFetchProbe.Data;

/// <summary>Bound from the "Database" section of appsettings.json (normally derived from Paths:DataRoot).</summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    /// <summary>The SQLite file (&lt;DataRoot&gt;\database\jobs.db). Created, with its folder, on first use.</summary>
    public string Path { get; set; } = System.IO.Path.Combine("database", "jobs.db");

    public string FullPath => System.IO.Path.GetFullPath(Path);
}
