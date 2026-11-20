using Microsoft.AspNetCore.Diagnostics;

namespace MunicipalElections.Web.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError("iOS API request failed with {ErrorType}; trace {TraceId}",
            exception.GetType().Name, context.TraceIdentifier);
        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
            title: "The election service could not complete this request. Please try again.",
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}
