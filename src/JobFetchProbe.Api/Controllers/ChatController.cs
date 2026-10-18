using JobFetchProbe.Models.Chat;
using JobFetchProbe.Services.Chat;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Controllers;

/// <summary>
/// One turn with the local model. The message is checked, sent, and forgotten. Everything goes through
/// <see cref="IChatService"/>. A blank or too long message is a 400. Ollama being down or too slow is a 503 from
/// <c>LocalChatUnavailableHandler</c>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ChatController(IChatService chat) : ControllerBase
{
    /// <summary>Sends one message and returns the answer, with the name of the model that wrote it.</summary>
    /// <remarks>POST api/Chat { "message": "Say hello in one sentence." }</remarks>
    /// <response code="200">The answer text and the model name.</response>
    /// <response code="400">No message, a blank message, or more than 20000 characters.</response>
    /// <response code="503">Ollama is not running, timed out, or returned an answer this app could not read.</response>
    [HttpPost]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatResponse>> Send(ChatRequest request, CancellationToken ct = default)
    {
        try
        {
            return await chat.SendAsync(request, ct);
        }
        catch (ChatRequestException ex)
        {
            ModelState.AddModelError(nameof(request.Message), ex.Message);
            return ValidationProblem(ModelState);
        }
    }
}
