using JobFetchProbe.Domain.Models;

namespace JobFetchProbe.Application.Abstractions;

public interface IJobExtractor
{
    /// <summary>Reads one local HTML file.</summary>
    /// <exception cref="FormatException">The file does not satisfy the contract. The message names the field.</exception>
    JobPosting Extract(string htmlPath);
}
