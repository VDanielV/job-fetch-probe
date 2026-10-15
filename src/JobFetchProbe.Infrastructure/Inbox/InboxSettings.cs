namespace JobFetchProbe.Infrastructure.Inbox;

/// <summary>Bound from the "Inbox" section of appsettings.json.</summary>
public sealed class InboxSettings
{
    public const string SectionName = "Inbox";

    /// <summary>Where saved search pages are dropped. Created if missing.</summary>
    public string Folder { get; set; } = "in";

    /// <summary>Where pages are moved after they were read and the batch was written. May be inside <see cref="Folder"/>.</summary>
    public string ArchiveFolder { get; set; } = Path.Combine("in", "archived");

    /// <summary>Folder for the &lt;yyyyMMdd_HHmmss&gt;.json batch files.</summary>
    public string OutputFolder { get; set; } = "job-searches";

    /// <summary>Process the inbox every time the app starts, before anything else.</summary>
    public bool ProcessOnStart { get; set; } = true;

    /// <summary>File patterns read from the inbox.</summary>
    public string[] Patterns { get; set; } = ["*.html", "*.htm"];
}
