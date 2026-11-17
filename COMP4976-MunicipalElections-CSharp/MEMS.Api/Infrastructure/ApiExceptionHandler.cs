using System.ComponentModel.DataAnnotations;
using MEMS.Data.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var (status, title, detail) = error switch
        {
            UnauthorizedAccessException => (403, "Forbidden", "This operation is outside your permitted scope."),
            RecordConflictException => (409, "Record conflict", "A duplicate or dependent record prevents this operation."),
            ValidationException => (400, "Invalid request", "One or more supplied values or related records are invalid."),
            ArgumentException => (400, "Invalid request", "One or more request parameters are invalid."),
            DbUpdateConcurrencyException => (409, "Record changed", "Reload the record and retry with its current revision."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                (409, "Duplicate record", "A record with the same unique values already exists."),
            DbUpdateException { InnerException: SqlException { Number: 547 } } =>
                (409, "Related records conflict", "Check related records before changing or deleting this record."),
            SqlException => (503, "Database unavailable", "The database is temporarily unavailable."),
            _ => (500, "Server error", "The request could not be completed.")
        };
        // No request bodies, credentials, query strings, exception messages, or connection strings are logged.
        logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning,
            "API failure {ErrorType} with status {Status} at {Method} {Path}, trace {TraceId}",
            error.GetType().Name, status, context.Request.Method, context.Request.Path, context.TraceIdentifier);
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: title, detail: detail,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}
