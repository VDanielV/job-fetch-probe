using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace JobFetchProbe.Extract;

/// <summary>
/// Reads the synthetic contract only: an element with <c>data-probe="job"</c> containing
/// elements with <c>data-field</c> = title, company, location, description.
/// It has no knowledge of LinkedIn markup.
/// </summary>
public sealed partial class FixtureJobExtractor : IJobExtractor
{
    public const int MaxShortField = 200;
    public const int MinDescription = 40;

    public JobPosting Extract(string htmlPath) =>
        ExtractFromHtml(File.ReadAllText(htmlPath), htmlPath);

    public static JobPosting ExtractFromHtml(string html, string sourcePath)
    {
        var sourceName = Path.GetFileName(sourcePath);
        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using var text = new StringReader(html);
            using var reader = XmlReader.Create(text, settings);
            doc = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new FormatException(
                $"{sourceName} is not well-formed markup for the fixture extractor (line {ex.LineNumber}); " +
                "it reads only files that follow the data-field contract");
        }

        var root = doc.Descendants().FirstOrDefault(e => (string?)e.Attribute("data-probe") == "job")
                   ?? doc.Root
                   ?? throw new FormatException($"{sourceName} has no root element");

        string Field(string name)
        {
            var el = root.DescendantsAndSelf().FirstOrDefault(e => (string?)e.Attribute("data-field") == name);
            return el is null ? string.Empty : Whitespace().Replace(el.Value, " ").Trim();
        }

        var title = Require("title", Field("title"), 1, MaxShortField);
        var company = Require("company", Field("company"), 1, MaxShortField);
        var location = Require("location", Field("location"), 1, int.MaxValue);
        var description = Require("description", Field("description"), MinDescription, int.MaxValue);

        return new JobPosting(title, company, location, description, sourceName);
    }

    private static string Require(string field, string value, int min, int max)
    {
        if (value.Length == 0)
        {
            throw new FormatException($"{field} is missing or empty");
        }
        if (value.Length < min)
        {
            throw new FormatException($"{field} is shorter than {min} characters");
        }
        if (value.Length > max)
        {
            throw new FormatException($"{field} is longer than {max} characters");
        }
        return value;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
