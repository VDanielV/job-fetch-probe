using JobFetchProbe.Services.Catalog;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobFetchProbe.Api.Handlers;

/// <summary>
/// Turns <see cref="JobStoreUnavailableException"/> (the database does not exist yet, as reported by the data layer)
/// into a 503 problem details response, for every endpoint. Other exceptions are left to the default handling.
/// </summary>
public sealed class JobStoreUnavailableHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not JobStoreUnavailableException)
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
                Title = "No database yet",
                Detail = exception.Message,
            },
        });
    }
}
