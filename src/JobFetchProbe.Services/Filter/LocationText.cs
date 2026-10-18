using System.Text.RegularExpressions;
using JobFetchProbe.Models.Filter;

namespace JobFetchProbe.Services.Filter;

/// <summary>
/// Reads a job's location text as LinkedIn wrote it ("Valencia/València (Híbrido)", "Paterna, Valencian Community,
/// Spain", "España") into a place and a work mode, so that spelling variants count as one place. Pure functions.
/// </summary>
internal static partial class LocationText
{
    public const string CountryOnlyKey = "spain";
    public const string NoLocationKey = "none";

    /// <summary>Spellings of the same place, compared without accents and capitals: variant → the name to use.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["almusafes"] = "Almussafes",
        ["picasent"] = "Picassent",
        ["sagunt"] = "Sagunto",
        ["puzol"] = "Puçol",
    };

    /// <summary>The country alone: the job names no city.</summary>
    private static readonly HashSet<string> CountryNames = new(StringComparer.Ordinal) { "spain", "espana" };

    public static LocationReading Read(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return new LocationReading(NoLocationKey, "No location", WorkMode.NotStated);
        }

        var mode = ModeOf(TextFilter.Fold(location));
        var place = Brackets().Replace(location, " ");
        place = place.Split(',')[0];
        place = place.Split('/')[0];
        place = Spaces().Replace(place, " ").Trim();

        var greater = Greater().Match(place);
        if (greater.Success)
        {
            place = greater.Groups[1].Value.Trim();
        }
        var around = Around().Match(place);
        if (around.Success)
        {
            place = around.Groups[1].Value.Trim();
        }

        var folded = TextFilter.Fold(place);
        if (folded.Length == 0 || CountryNames.Contains(folded))
        {
            return new LocationReading(CountryOnlyKey, "Spain, no city", mode);
        }
        if (Aliases.TryGetValue(folded, out var canonical))
        {
            place = canonical;
            folded = TextFilter.Fold(canonical);
        }
        return new LocationReading(Spaces().Replace(folded, "-"), place, mode);
    }

    /// <summary>The address value of a work mode ("hybrid"), and back.</summary>
    public static string KeyOf(WorkMode mode)
    {
        return mode.ToString().ToLowerInvariant();
    }

    public static bool TryParseMode(string? key, out WorkMode mode)
    {
        mode = WorkMode.NotStated;
        if (string.IsNullOrWhiteSpace(key) || int.TryParse(key, out _))
        {
            return false;
        }
        return Enum.TryParse(key.Trim(), ignoreCase: true, out mode) && Enum.IsDefined(mode);
    }

    public static string LabelOf(WorkMode mode)
    {
        switch (mode)
        {
            case WorkMode.Remote:
                return "Remote";
            case WorkMode.Hybrid:
                return "Hybrid";
            case WorkMode.OnSite:
                return "On-site";
            default:
                return "Not stated";
        }
    }

    private static WorkMode ModeOf(string folded)
    {
        if (folded.Contains("hibrid", StringComparison.Ordinal) || folded.Contains("hybrid", StringComparison.Ordinal))
        {
            return WorkMode.Hybrid;
        }
        if (folded.Contains("remot", StringComparison.Ordinal))
        {
            return WorkMode.Remote;
        }
        if (folded.Contains("presencial", StringComparison.Ordinal) || folded.Contains("on-site", StringComparison.Ordinal) || folded.Contains("onsite", StringComparison.Ordinal))
        {
            return WorkMode.OnSite;
        }
        return WorkMode.NotStated;
    }

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^Greater\s+(.+?)\s+Area$", RegexOptions.IgnoreCase)]
    private static partial Regex Greater();

    [GeneratedRegex(@"^(.+?)\s+y\s+alrededores$", RegexOptions.IgnoreCase)]
    private static partial Regex Around();
}
