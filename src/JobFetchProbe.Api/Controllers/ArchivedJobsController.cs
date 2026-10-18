using JobFetchProbe.Models.Archive;
using JobFetchProbe.Services.Archive;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// Archives jobs the owner will not apply to: they leave the Overview and step 2 never requests them again. Nothing is
/// deleted. Everything goes through <see cref="IJobArchiver"/>; a missing database (503) is answered by
/// <c>JobStoreUnavailableHandler</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ArchivedJobsController(IJobArchiver archiver) : ControllerBase
{
    /// <summary>Archives one or more jobs, all with the same reason and note, in one transaction.</summary>
    /// <remarks>POST api/ArchivedJobs { "jobIds": ["4477387672"], "reason": "TechStack", "note": "Databricks" }</remarks>
    /// <response code="200">What was done: archived now, already archived (unchanged), not found.</response>
    /// <response code="400">No job, more than 100, an unknown reason, Other without a note, or a note over 500 characters.</response>
    [HttpPost]
    [ProducesResponseType<ArchiveResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ArchiveResult>> Archive(ArchiveRequest request, CancellationToken ct = default)
    {
        try
        {
            return await archiver.ArchiveAsync(request, ct);
        }
        catch (ArchiveRequestException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            return ValidationProblem(ModelState);
        }
    }
}
