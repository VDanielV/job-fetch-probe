using JobFetchProbe.Services.Chat;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Handlers;

/// <summary>
/// Turns <see cref="LocalChatUnavailableException"/> (Ollama is not running, timed out, or answered something this
/// app could not read) into a 503 problem details response. Other exceptions are left to the handlers registered after.
/// </summary>
public sealed class LocalChatUnavailableHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not LocalChatUnavailableException)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Ollama is not available",
                Detail = exception.Message,
            },
        });
    }
}
