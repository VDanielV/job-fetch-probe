using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Handlers;

/// <summary>
/// Every error the other handlers leave: a 500 problem details response with the error's type and message (for
/// example a locked database), so the web page can say what went wrong. The API runs on the owner's computer only,
/// so the message is shown as it is. It is also written to the API's log.
/// </summary>
public sealed class UnexpectedErrorHandler(IProblemDetailsService problems, ILogger<UnexpectedErrorHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var error = exception.GetBaseException();
        logger.LogError(exception, "{Method} {Path} failed: {Error}", context.Request.Method, context.Request.Path, error.Message);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "The API could not complete the request",
            Detail = error.Message,
        };
        problem.Extensions["errorType"] = error.GetType().FullName;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
