namespace JobFetchProbe.Extract;

public interface IJobExtractor
{
    /// <summary>Reads one local HTML file. Throws <see cref="FormatException"/> naming the field that failed.</summary>
    JobPosting Extract(string htmlPath);
}
