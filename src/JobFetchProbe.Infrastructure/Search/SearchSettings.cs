namespace JobFetchProbe.Infrastructure.Search;

/// <summary>Bound from the "Search" section of appsettings.json.</summary>
public sealed class SearchSettings
{
    public const string SectionName = "Search";

    /// <summary>Folder for the &lt;SearchId&gt;.json files. A relative path is resolved from the folder you run the app in.</summary>
    public string OutputFolder { get; set; } = "job-searches";

    /// <summary>Search page used when the search command is given without a link or file. Optional.</summary>
    public string? DefaultUrl { get; set; }
}
