using System.ComponentModel.DataAnnotations;
using JobFetchProbe.Models.RunHistory;
using JobFetchProbe.Services.RunHistory;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// The history of past acquisitions, read only: one line per acquisition, and every job link of one with whether it
/// was new and what step 2 did with it. Everything goes through <see cref="IRunHistoryService"/>; a missing database
/// (503) is answered by <c>JobStoreUnavailableHandler</c>. A run's journal itself is under api/Journal.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class RunsController(IRunHistoryService history) : ControllerBase
{
    /// <summary>One page of past acquisitions, newest first.</summary>
    /// <param name="includeEmpty">Also the acquisitions that did nothing (no saved page, nothing for step 2).</param>
    /// <remarks>GET api/Runs?page=1&amp;pageSize=25&amp;includeEmpty=false</remarks>
    [HttpGet]
    [ProducesResponseType<RunHistoryPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RunHistoryPage>> GetRuns(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        [FromQuery] bool includeEmpty = false,
        CancellationToken ct = default)
    {
        return await history.GetPageAsync(page, pageSize, includeEmpty, ct);
    }

    /// <summary>One acquisition, by its step 1 run's journal ID.</summary>
    /// <remarks>GET api/Runs/16</remarks>
    [HttpGet("{runId:long}")]
    [ProducesResponseType<RunHistoryLine>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RunHistoryLine>> GetRun(long runId, CancellationToken ct = default)
    {
        var run = await history.GetRunAsync(runId, ct);
        if (run is null)
        {
            return RunNotFound(runId.ToString());
        }
        return run;
    }

    /// <summary>The job links of an acquisition: new or already known, the job now, and what step 2 did with it.</summary>
    /// <remarks>GET api/Runs/16/job-links</remarks>
    [HttpGet("{runId:long}/job-links")]
    [ProducesResponseType<RunJobLinks>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RunJobLinks>> GetJobLinks(long runId, CancellationToken ct = default)
    {
        var links = await history.GetJobLinksAsync(runId, ct);
        if (links is null)
        {
            return RunNotFound(runId.ToString());
        }
        return links;
    }

    /// <summary>The job links of a listing stored before the journal existed.</summary>
    /// <remarks>GET api/Runs/listings/20261016_134541/job-links</remarks>
    [HttpGet("listings/{listingId}/job-links")]
    [ProducesResponseType<RunJobLinks>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RunJobLinks>> GetListingJobLinks(string listingId, CancellationToken ct = default)
    {
        var links = await history.GetListingJobLinksAsync(listingId, ct);
        if (links is null)
        {
            return RunNotFound(listingId);
        }
        return links;
    }

    private ObjectResult RunNotFound(string what)
    {
        return Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found", detail: $"There is no acquisition {what}.");
    }
}
