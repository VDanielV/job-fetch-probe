namespace JobFetchProbe.Domain.Models;

/// <summary>
/// A job read from a local file. Instances exist only through <see cref="Create"/>,
/// which enforces the field contract from the spec.
/// </summary>
public sealed record JobPosting
{
    public const int MaxShortField = 200;
    public const int MinDescription = 40;

    private JobPosting(string title, string company, string location, string description, string sourcePath)
    {
        Title = title;
        Company = company;
        Location = location;
        Description = description;
        SourcePath = sourcePath;
    }

    public string Title { get; }
    public string Company { get; }

    /// <summary>May be <c>Unspecified</c>.</summary>
    public string Location { get; }
    public string Description { get; }

    /// <summary>File name only, never a directory.</summary>
    public string SourcePath { get; }

    /// <exception cref="FormatException">A required field is missing, empty or outside its length range. The message names the field.</exception>
    public static JobPosting Create(string? title, string? company, string? location, string? description, string sourcePath) =>
        new(
            Require("title", title, 1, MaxShortField),
            Require("company", company, 1, MaxShortField),
            Require("location", location, 1, int.MaxValue),
            Require("description", description, MinDescription, int.MaxValue),
            Path.GetFileName(sourcePath));

    private static string Require(string field, string? raw, int min, int max)
    {
        var value = (raw ?? string.Empty).Trim();
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
}
