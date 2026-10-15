using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace JobFetchProbe.Console;

/// <summary>
/// One setting, Paths:DataRoot, places every folder and file the app reads or writes:
/// <code>
/// &lt;DataRoot&gt;\database\jobs.db          Storage:Sqlite:Path
/// &lt;DataRoot&gt;\input\                    Inbox:Folder         (save search result pages here)
/// &lt;DataRoot&gt;\input\archived\           Inbox:ArchiveFolder
/// &lt;DataRoot&gt;\pages\                    (job pages you saved, for the job step; not read automatically)
/// &lt;DataRoot&gt;\output\jobs\              Output:Folder          (JSON storage only)
/// &lt;DataRoot&gt;\output\listings\          Inbox:OutputFolder     (JSON storage only)
/// &lt;DataRoot&gt;\output\descriptions\      Details:OutputFolder   (JSON storage only)
/// &lt;DataRoot&gt;\output\searches\          Search:OutputFolder    (JSON storage only)
/// </code>
/// These are defaults with the lowest priority: a folder set explicitly in appsettings.json or an environment
/// variable still wins, so any one of them can be moved on its own.
/// </summary>
public static class DataPaths
{
    public const string RootKey = "Paths:DataRoot";

    public static readonly IReadOnlyDictionary<string, string> Layout = new Dictionary<string, string>
    {
        ["Storage:Sqlite:Path"] = Path.Combine("database", "jobs.db"),
        ["Inbox:Folder"] = "input",
        ["Inbox:ArchiveFolder"] = Path.Combine("input", "archived"),
        ["Output:Folder"] = Path.Combine("output", "jobs"),
        ["Inbox:OutputFolder"] = Path.Combine("output", "listings"),
        ["Details:OutputFolder"] = Path.Combine("output", "descriptions"),
        ["Search:OutputFolder"] = Path.Combine("output", "searches"),
    };

    /// <summary>Folders created at start-up so the user can see where to put things.</summary>
    public static readonly IReadOnlyList<string> CreatedOnStart = ["input", Path.Combine("input", "archived"), "pages"];

    /// <summary>
    /// Adds the folders derived from Paths:DataRoot underneath every other configuration source.
    /// Returns the full data root, or null if Paths:DataRoot is not set (then each folder setting is used as is).
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

    /// <summary>Creates the data root's input folders, so they exist before the first page is saved.</summary>
    public static void CreateFolders(string root)
    {
        foreach (var folder in CreatedOnStart)
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }
    }
}
