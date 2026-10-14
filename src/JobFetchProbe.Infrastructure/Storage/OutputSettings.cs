namespace JobFetchProbe.Infrastructure.Storage;

/// <summary>Bound from the "Output" section of appsettings.json.</summary>
public sealed class OutputSettings
{
    public const string SectionName = "Output";

    /// <summary>Folder for the &lt;JobId&gt;.json files. A relative path is resolved from the folder you run the app in.</summary>
    public string Folder { get; set; } = "output";
}
