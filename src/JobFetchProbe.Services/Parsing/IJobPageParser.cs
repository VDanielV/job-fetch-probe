using JobFetchProbe.Models;

namespace JobFetchProbe.Services.Parsing;

public interface IJobPageParser
{
    /// <summary>Reads the job fields from a page. JobId, SourceUrl and RetrievedAt are filled in by the caller.</summary>
    JobPosting Parse(string html);
}
