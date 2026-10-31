using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CipherRAG.Infrastructure.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? traceId;

        _logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, CorrelationId: {CorrelationId}, RequestPath: {Path}",
            traceId,
            correlationId,
            httpContext.Request.Path);

        var (statusCode, title, detail) = exception switch
        {
            KeyNotFoundException ex => (StatusCodes.Status404NotFound, "Resource Not Found", ex.Message),
            UnauthorizedAccessException ex => (StatusCodes.Status401Unauthorized, "Unauthorized Access", ex.Message),
            ArgumentException ex => (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),
            InvalidOperationException ex when ex.Message.Contains("No database provider") || ex.Message.Contains("ConnectionString property has not been initialized")
                => (StatusCodes.Status500InternalServerError, "Database Not Configured", "Database connection string is missing. Please set 'ConnectionStrings__DefaultConnection' in Render Environment Variables."),
            InvalidOperationException ex => (StatusCodes.Status400BadRequest, "Invalid Operation", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "An internal server error occurred while processing your request.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
