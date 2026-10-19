using JobFetchProbe.Models.Applications;
using JobFetchProbe.Services.Applications;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// The jobs the owner applied to: "I applied", undo, the list, the stage and the notes. Everything goes through
/// <see cref="IApplicationTracker"/>; a missing database (503) is answered by <c>JobStoreUnavailableHandler</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ApplicationsController(IApplicationTracker applications) : ControllerBase
{
    public const int MaxPageSize = 100;

    /// <summary>"I applied": the job leaves the Overview and starts at stage Applied.</summary>
    /// <remarks>POST api/Applications { "jobId": "4476446382" }</remarks>
    /// <response code="201">The new application with its first timeline line.</response>
    /// <response code="400">No job ID.</response>
    /// <response code="404">That job is not in the database.</response>
    /// <response code="409">The job is already applied to, or archived.</response>
    [HttpPost]
    [ProducesResponseType<Application>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Application>> Apply(ApplyRequest request, CancellationToken ct = default)
    {
        try
        {
            var created = await applications.ApplyAsync(request, ct);
            if (created is null)
            {
                return JobNotFound(request.JobId);
            }
            return CreatedAtAction(nameof(Get), new { jobId = created.Summary.JobId }, created);
        }
        catch (ApplicationRequestException ex)
        {
            return Invalid(ex);
        }
        catch (ApplicationConflictException ex)
        {
            return ConflictProblem(ex);
        }
    }

    /// <summary>
    /// Undo of a mistaken "I applied": deletes the application and its timeline; the job is back in the Overview. When the
    /// owner wrote notes, only with withNotes=true.
    /// </summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">There is no application for that job.</response>
    /// <response code="409">The owner wrote notes and withNotes is not true; the problem's "notes" says how many.</response>
    [HttpDelete("{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Undo(string jobId, [FromQuery] bool withNotes = false, CancellationToken ct = default)
    {
        try
        {
            if (!await applications.UndoAsync(jobId, withNotes, ct))
            {
                return NotApplied(jobId);
            }
            return NoContent();
        }
        catch (ApplicationConflictException ex)
        {
            return ConflictProblem(ex);
        }
    }

    /// <summary>
    /// One page of the open applications (or closed=true), optionally one stage or only the flagged ones; sort=Fit lists
    /// the best latest fit first.
    /// </summary>
    /// <remarks>GET api/Applications?closed=false&amp;stage=Interview&amp;flagged=false&amp;sort=FollowUp&amp;page=1&amp;pageSize=25</remarks>
    /// <response code="200">The page, with the counts for the chips.</response>
    /// <response code="400">A page below 1, or a page size outside 1–100.</response>
    [HttpGet]
    [ProducesResponseType<ApplicationPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApplicationPage>> List(
        [FromQuery] bool closed = false, [FromQuery] ApplicationStage? stage = null, [FromQuery] bool flagged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] ApplicationSort sort = ApplicationSort.FollowUp,
        CancellationToken ct = default)
    {
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "The page must be 1 or more.");
        }
        if (pageSize is < 1 or > MaxPageSize)
        {
            ModelState.AddModelError(nameof(pageSize), $"The page size must be 1-{MaxPageSize}.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        return await applications.GetPageAsync(closed, stage, flagged, page, pageSize, sort, ct);
    }

    /// <summary>The numbers for the menu: open applications, and how many are flagged.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<ApplicationsSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApplicationsSummary>> Summary(CancellationToken ct = default)
    {
        return await applications.GetSummaryAsync(ct);
    }

    /// <summary>One application with its timeline, newest line first.</summary>
    /// <response code="404">There is no application for that job.</response>
    [HttpGet("{jobId}")]
    [ProducesResponseType<Application>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Application>> Get(string jobId, CancellationToken ct = default)
    {
        var application = await applications.GetAsync(jobId, ct);
        if (application is null)
        {
            return NotApplied(jobId);
        }
        return application;
    }

    /// <summary>Moves the application to another stage and adds a line to its timeline.</summary>
    /// <remarks>PUT api/Applications/4476446382/stage { "stage": "Interview" }</remarks>
    /// <response code="400">An unknown stage.</response>
    /// <response code="404">There is no application for that job.</response>
    [HttpPut("{jobId}/stage")]
    [ProducesResponseType<Application>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Application>> SetStage(string jobId, StageRequest request, CancellationToken ct = default)
    {
        try
        {
            var application = await applications.SetStageAsync(jobId, request, ct);
            if (application is null)
            {
                return NotApplied(jobId);
            }
            return application;
        }
        catch (ApplicationRequestException ex)
        {
            return Invalid(ex);
        }
    }

    /// <summary>Adds a note to the timeline, dated now.</summary>
    /// <remarks>POST api/Applications/4476446382/notes { "text": "Recruiter called." }</remarks>
    /// <response code="400">An empty note, or one over 2000 characters.</response>
    /// <response code="404">There is no application for that job.</response>
    [HttpPost("{jobId}/notes")]
    [ProducesResponseType<Application>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Application>> AddNote(string jobId, NoteRequest request, CancellationToken ct = default)
    {
        try
        {
            var application = await applications.AddNoteAsync(jobId, request, ct);
            if (application is null)
            {
                return NotApplied(jobId);
            }
            return application;
        }
        catch (ApplicationRequestException ex)
        {
            return Invalid(ex);
        }
    }

    private ActionResult Invalid(ApplicationRequestException ex)
    {
        ModelState.AddModelError(ex.Field, ex.Message);
        return ValidationProblem(ModelState);
    }

    private ObjectResult ConflictProblem(ApplicationConflictException ex)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, StatusCodes.Status409Conflict, "Not possible now", detail: ex.Message);
        if (ex.Notes > 0)
        {
            problem.Extensions["notes"] = ex.Notes;
        }
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
    }

    private ObjectResult JobNotFound(string jobId)
    {
        return Problem(statusCode: StatusCodes.Status404NotFound, title: "Job not found", detail: $"There is no job {jobId} in the database.");
    }

    private ObjectResult NotApplied(string jobId)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound, title: "No application", detail: $"Job {jobId} is not in your applications.");
    }
}
