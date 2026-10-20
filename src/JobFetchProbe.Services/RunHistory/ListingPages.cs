using JobFetchProbe.Services.Abstractions.RunHistory;

namespace JobFetchProbe.Services.RunHistory;

/// <summary>
/// Which saved page each job link of a listing came from. Step 1 stores the links page after page, each page adding
/// those not on an earlier page, so the pages' counts cut the listing into consecutive ranges.
/// </summary>
internal static class ListingPages
{
    /// <returns>For each page, in order: its file and the first position after its range.</returns>
    public static List<(string File, int End)> Of(IReadOnlyList<ListingPageRecord> pages)
    {
        var ranges = new List<(string File, int End)>();
        var end = 0;
        foreach (var page in pages)
        {
            end += page.Unique;
            ranges.Add((page.File, end));
        }
        return ranges;
    }

    /// <param name="position">The link's position in the listing (0-based).</param>
    /// <returns>The page's file; null when the counts do not cover the position.</returns>
    public static string? PageAt(List<(string File, int End)> ranges, int position)
    {
        foreach (var (file, end) in ranges)
        {
            if (position < end)
            {
                return file;
            }
        }
        return null;
    }
}
