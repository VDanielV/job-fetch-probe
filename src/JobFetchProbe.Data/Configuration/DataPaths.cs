using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace JobFetchProbe.Data.Configuration;

/// <summary>
/// One setting, Paths:DataRoot, places every folder and file the apps read or write:
/// <code>
/// &lt;DataRoot&gt;\database\jobs.db      Database:Path
/// &lt;DataRoot&gt;\input\                FetchJobListing:Folder          (save search result pages here)
/// &lt;DataRoot&gt;\input\archived\       FetchJobListing:ArchiveFolder   (pages already read)
/// </code>
/// These are defaults with the lowest priority: a folder set explicitly in appsettings.json or an environment
/// variable still wins, so any one of them can be moved on its own.
/// </summary>
public static class DataPaths
{
    public const string RootKey = "Paths:DataRoot";

    public static readonly IReadOnlyDictionary<string, string> Layout = new Dictionary<string, string>
    {
        ["Database:Path"] = Path.Combine("database", "jobs.db"),
        ["FetchJobListing:Folder"] = "input",
        ["FetchJobListing:ArchiveFolder"] = Path.Combine("input", "archived"),
    };

    /// <summary>Folders created at start-up so the user can see where to put things.</summary>
    public static readonly IReadOnlyList<string> CreatedOnStart = ["input", Path.Combine("input", "archived")];

    /// <summary>
    /// Adds the folders derived from Paths:DataRoot underneath every other configuration source.
    /// Returns the full data root, or null if Paths:DataRoot is not set (then each setting is used as is).
    /// </summary>
    public static string? AddDataRootDefaults(IConfigurationManager configuration)
    {
        var configured = configuration[RootKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var root = Path.GetFullPath(configured.Trim());
        var defaults = Layout.ToDictionary(kv => kv.Key, kv => (string?)Path.Combine(root, kv.Value));

        // Index 0 = lowest priority: appsettings.json and environment variables override these.
        configuration.Sources.Insert(0, new MemoryConfigurationSource { InitialData = defaults });
        return root;
    }

    //TODO: This has nothing to do with database and should be renamed to InputPaths or something. But for now, keep it as is.
    /// <summary>Creates the data root's input folders, so they exist before the first page is saved.</summary>
    public static void CreateFolders(string root)
    {
        foreach (var folder in CreatedOnStart)
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }
    }
}
