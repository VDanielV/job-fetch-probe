using System.ComponentModel.DataAnnotations;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Journal;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// Reads the journal of the two steps, while they run and afterwards. Read-only; everything goes through
/// <see cref="IJournalService"/>. To follow a run: ask for it with after=0, then again every few seconds with the last
/// entry id received, until its state is no longer Running (Completed, Stopped, Failed or Abandoned).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class JournalController(IJournalService journal) : ControllerBase
{
    /// <summary>The newest runs of both steps, newest first.</summary>
    /// <param name="take">How many, 1 to 50.</param>
    /// <remarks>GET api/Journal/runs?take=10</remarks>
    [HttpGet("runs")]
    [ProducesResponseType<IReadOnlyList<JournalRunSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<JournalRunSummary>>> GetRuns(
        [FromQuery, Range(1, JournalService.MaxTake)] int take = 10,
        CancellationToken ct = default)
    {
        var runs = await journal.GetRecentRunsAsync(take, ct);
        return Ok(runs);
    }

    /// <summary>A step 1 run (FetchJobListing) and its entries after the given entry id.</summary>
    /// <param name="runId">The run's id, from GET api/Journal/runs.</param>
    /// <param name="after">The last entry id already received; 0 for all entries.</param>
    /// <remarks>GET api/Journal/job-listing/runs/12?after=0</remarks>
    [HttpGet("job-listing/runs/{runId:long}")]
    [ProducesResponseType<JobListingJournalRun>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<JobListingJournalRun>> GetJobListingRun(
        long runId,
        [FromQuery, Range(0, int.MaxValue)] long after = 0,
        CancellationToken ct = default)
    {
        var run = await journal.GetJobListingRunAsync(runId, after, ct);
        if (run is null)
        {
            return RunNotFound("job listing", runId);
        }
        return run;
    }

    /// <summary>A step 2 run (FetchJobDetails) and its entries after the given entry id.</summary>
    /// <param name="runId">The run's id, from GET api/Journal/runs.</param>
    /// <param name="after">The last entry id already received; 0 for all entries.</param>
    /// <remarks>GET api/Journal/job-details/runs/7?after=0</remarks>
    [HttpGet("job-details/runs/{runId:long}")]
    [ProducesResponseType<JobDetailsJournalRun>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<JobDetailsJournalRun>> GetJobDetailsRun(
        long runId,
        [FromQuery, Range(0, int.MaxValue)] long after = 0,
        CancellationToken ct = default)
    {
        var run = await journal.GetJobDetailsRunAsync(runId, after, ct);
        if (run is null)
        {
            return RunNotFound("job details", runId);
        }
        return run;
    }

    private ObjectResult RunNotFound(string step, long runId)
    {
        return Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found", detail: $"There is no {step} run {runId} in the journal.");
    }
}
