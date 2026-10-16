using System.ComponentModel.DataAnnotations;
using JobFetchProbe.Models.Catalog;
using JobFetchProbe.Services.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// The stored jobs, read-only: one page of the list, or one job in full. Everything goes through <see cref="IJobRetriever"/>;
/// a missing database (503) is reported by the data layer and turned into a response by <c>JobStoreUnavailableHandler</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class JobCatalogController(IJobRetriever jobs) : ControllerBase
{
    /// <summary>One page of jobs, newest job IDs first.</summary>
    /// <param name="page">1-based page number. A page past the end returns the last page; the response says which page it is.</param>
    /// <param name="pageSize">Jobs per page, 1 to 100.</param>
    /// <remarks>GET api/JobCatalog?page=1&amp;pageSize=25</remarks>
    [HttpGet]
    [ProducesResponseType<JobSummaryPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<JobSummaryPage>> GetPage(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, JobRetriever.MaxPageSize)] int pageSize = JobRetriever.DefaultPageSize,
        CancellationToken ct = default)
    {
        return await jobs.GetPageAsync(page, pageSize, ct);
    }

    /// <summary>One job: its details, description and status.</summary>
    /// <param name="jobId">The job ID, e.g. 4476446382.</param>
    /// <remarks>GET api/JobCatalog/4476446382</remarks>
    [HttpGet("{jobId}")]
    [ProducesResponseType<StoredJob>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StoredJob>> GetJob(string jobId, CancellationToken ct = default)
    {
        var job = await jobs.GetJobAsync(jobId, ct);
        if (job is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Job not found", detail: $"There is no job {jobId} in the database.");
        }
        else
        {
            return job;
        }
    }
}
