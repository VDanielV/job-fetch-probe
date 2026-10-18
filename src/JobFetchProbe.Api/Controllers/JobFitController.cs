using JobFetchProbe.Models.Fit;
using JobFetchProbe.Services.Fit;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// Scores one stored job against the Fit files. Everything goes through <see cref="IJobFitService"/>.
/// Nothing is saved. A missing description is a 400. An unknown id is a 404. Ollama being down, too slow,
/// or returning a score this app cannot read is a 503 from <c>LocalChatUnavailableHandler</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class JobFitController(IJobFitService fits) : ControllerBase
{
    /// <summary>Scores the job and returns the decision, the fit, and the reason.</summary>
    /// <remarks>POST api/JobFit { "jobId": "4476446382" }</remarks>
    /// <response code="200">Apply, Maybe, or Skip, with a fit from 0 to 100.</response>
    /// <response code="400">No job id, or the job has no description yet.</response>
    /// <response code="404">That id is not in the database.</response>
    /// <response code="503">The database is missing, or Ollama cannot answer.</response>
    [HttpPost]
    [ProducesResponseType<JobFitResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<JobFitResult>> Assess(JobFitRequest request, CancellationToken ct = default)
    {
        try
        {
            var result = await fits.AssessAsync(request, ct);
            if (result is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Job not found",
                    detail: $"There is no job {request.JobId} in the database.");
            }

            return result;
        }
        catch (JobFitRequestException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            return ValidationProblem(ModelState);
        }
    }
}
