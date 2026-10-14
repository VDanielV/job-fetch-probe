using System.Xml;
using System.Xml.Linq;
using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Domain.Models;
using JobFetchProbe.Domain.Rules;

namespace JobFetchProbe.Infrastructure.Extraction;

/// <summary>
/// Reads the synthetic contract only: an element with <c>data-probe="job"</c> containing
/// elements with <c>data-field</c> = title, company, location, description.
/// It has no knowledge of LinkedIn markup. Field rules are enforced by <see cref="JobPosting.Create"/>.
/// </summary>
public sealed class FixtureJobExtractor : IJobExtractor
{
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

        string? Field(string name)
        {
            var el = root.DescendantsAndSelf().FirstOrDefault(e => (string?)e.Attribute("data-field") == name);
            return el is null ? null : HtmlText.CollapseWhitespace(el.Value);
        }

        return JobPosting.Create(Field("title"), Field("company"), Field("location"), Field("description"), sourcePath);
    }
}
