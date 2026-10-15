using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using JobFetchProbe.Services.Abstractions;

namespace JobFetchProbe.Infrastructure.Files;

/// <summary>
/// Reads a page saved from the browser. Accepts a normal "Save page as" file and Chrome's
/// "view-source:" save, which wraps the original HTML in a numbered table; that wrapper is removed.
/// </summary>
public sealed partial class SavedPageReader : ISavedPageReader
{
    public async Task<string> ReadAsync(string path, CancellationToken ct = default)
    {
        var html = await File.ReadAllTextAsync(path, ct);
        return IsViewSource(html) ? Unwrap(html) : html;
    }

    private static bool IsViewSource(string html) =>
        html.Contains("line-gutter-backdrop", StringComparison.Ordinal) &&
        html.Contains("<td class=\"line-content\">", StringComparison.Ordinal);

    private static string Unwrap(string html)
    {
        var source = new StringBuilder();

        // Keep the browser's "saved from url=…" note (it sits in the wrapper, not in the original HTML),
        // so the page's address is still known after unwrapping.
        if (SavedFromNote().Match(html) is { Success: true } note)
        {
            source.AppendLine(note.Value);
        }

        foreach (Match m in LineCells().Matches(html))
        {
            var line = m.Groups[1].Value.Replace("<br>", string.Empty);
            source.AppendLine(WebUtility.HtmlDecode(Tags().Replace(line, string.Empty)));
        }
        return source.ToString();
    }

    [GeneratedRegex(@"<td class=""line-content"">(.*?)</td></tr>", RegexOptions.Singleline)]
    private static partial Regex LineCells();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"<!--\s*saved from url=\(\d+\)\S+?\s*-->", RegexOptions.IgnoreCase)]
    private static partial Regex SavedFromNote();
}
