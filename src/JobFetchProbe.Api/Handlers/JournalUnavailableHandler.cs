using JobFetchProbe.Services.Journal;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Handlers;

/// <summary>Turns <see cref="JournalUnavailableException"/> (no database yet) into a 503 problem details response.</summary>
public sealed class JournalUnavailableHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not JournalUnavailableException)
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
                Title = "No journal yet",
                Detail = exception.Message,
            },
        });
    }
}
