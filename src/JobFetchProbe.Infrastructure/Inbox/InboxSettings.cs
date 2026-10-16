namespace JobFetchProbe.Infrastructure.Inbox;

/// <summary>Bound from the "Inbox" section of appsettings.json (both folders normally come from Paths:DataRoot).</summary>
public sealed class InboxSettings
{
    public const string SectionName = "Inbox";

    /// <summary>Where saved search pages are dropped (&lt;DataRoot&gt;\input). Created if missing.</summary>
    public string Folder { get; set; } = "input";

    /// <summary>Where pages are moved after they were read and stored (&lt;DataRoot&gt;\input\archived).</summary>
    public string ArchiveFolder { get; set; } = Path.Combine("input", "archived");

    /// <summary>File patterns read from the input folder.</summary>
    public string[] Patterns { get; set; } = ["*.html", "*.htm"];
}
