using JobFetchProbe.Models.Jobs;


namespace JobFetchProbe.Services.FetchJobDetails;

public interface IJobPageParser
{
    /// <summary>Reads the job fields from a page. RetrievedAt is filled in by the caller.</summary>
    JobPosting Parse(string html, string jobId, Uri sourceUrl);
}
