using JobFetchProbe.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace JobFetchProbe.Infrastructure.Inbox;

/// <summary>The inbox on disk. Only files directly in Inbox:Folder are read, so the archive subfolder is never re-read.</summary>
public sealed class InboxFolder(IOptions<InboxSettings> settings, TimeProvider time) : IInboxFolder
{
    private const string BrowserAssetsSuffix = "_files";

    public IReadOnlyList<string> ListPending()
    {
        var folder = Path.GetFullPath(settings.Value.Folder);
        Directory.CreateDirectory(folder);

        return settings.Value.Patterns
            .SelectMany(p => Directory.EnumerateFiles(folder, p, SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new FileInfo(p))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => f.FullName)
            .ToList();
    }

    public string Archive(string path)
    {
        var archive = Path.GetFullPath(settings.Value.ArchiveFolder);
        Directory.CreateDirectory(archive);

        var target = FreeName(archive, Path.GetFileName(path), isDirectory: false);
        File.Move(path, target);

        // "Save page as → Webpage, Complete" writes images and scripts to "<name>_files" next to the page.
        var assets = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + BrowserAssetsSuffix);
        if (Directory.Exists(assets))
        {
            var assetsTarget = Path.Combine(archive, Path.GetFileNameWithoutExtension(target) + BrowserAssetsSuffix);
            Directory.Move(assets, FreeName(archive, Path.GetFileName(assetsTarget), isDirectory: true));
        }

        return target;
    }

    /// <summary>The name itself if free, else "&lt;name&gt;_&lt;yyyyMMdd_HHmmss&gt;", then "_2", "_3"… so nothing in the archive is overwritten.</summary>
    private string FreeName(string folder, string name, bool isDirectory)
    {
        bool Exists(string p) => isDirectory ? Directory.Exists(p) : File.Exists(p);

        var candidate = Path.Combine(folder, name);
        if (!Exists(candidate))
        {
            return candidate;
        }

        var stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        var ext = isDirectory ? string.Empty : Path.GetExtension(name);
        var stamp = time.GetLocalNow().ToString("yyyyMMdd_HHmmss");
        candidate = Path.Combine(folder, $"{stem}_{stamp}{ext}");
        for (var n = 2; Exists(candidate); n++)
        {
            candidate = Path.Combine(folder, $"{stem}_{stamp}_{n}{ext}");
        }
        return candidate;
    }
}
