using JobFetchProbe.Models.Acquisition;
using JobFetchProbe.Models.Journal;
using JobFetchProbe.Services.Acquisition;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// Starts and stops an acquisition: step 1 (saved search pages in the input folder → job links), then step 2 (every job without
/// a description → its description), the same services the console app uses. It runs in the background in this
/// process; the request is answered at once. Its progress is in the journal (<c>GET api/Journal/runs</c>) and the
/// API's log. Everything goes through <see cref="IAcquisitionRunner"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class JobFetcherController(IAcquisitionRunner runner) : ControllerBase
{
    /// <summary>Starts an acquisition, unless one is already running (here or in the console app).</summary>
    /// <remarks>POST api/JobFetcher</remarks>
    /// <response code="202">Started. Follow it with GET api/Journal/runs (the Location header).</response>
    /// <response code="409">Not started: an acquisition is already running.</response>
    [HttpPost]
    [ProducesResponseType<AcquisitionStart>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AcquisitionStart>> Start(CancellationToken ct = default)
    {
        var start = await runner.TryStartAsync(JournalStarter.Api, ct);
        if (start.State == AcquisitionStartState.AlreadyRunning)
        {
            return Problem(title: "Acquisition already running", detail: start.Message, statusCode: StatusCodes.Status409Conflict);
        }
        return Accepted("/api/Journal/runs", start);
    }

    /// <summary>Stops the acquisition started here: after the current page or job, at once during a pause or a cooldown.</summary>
    /// <remarks>POST api/JobFetcher/stop</remarks>
    /// <response code="202">Stopping. The run ends Stopped; follow it with GET api/Journal/runs.</response>
    /// <response code="409">Nothing to stop here: no acquisition is running, or the console app started it.</response>
    [HttpPost("stop")]
    [ProducesResponseType<AcquisitionStop>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AcquisitionStop>> Stop(CancellationToken ct = default)
    {
        var stop = await runner.TryStopAsync(ct);
        if (stop.State == AcquisitionStopState.NotRunning)
        {
            return Problem(title: "Nothing to stop", detail: stop.Message, statusCode: StatusCodes.Status409Conflict);
        }
        return Accepted("/api/Journal/runs", stop);
    }
}
