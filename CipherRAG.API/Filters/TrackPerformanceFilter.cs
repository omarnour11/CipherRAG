using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace CipherRAG.API.Filters;

/// <summary>
/// Minimal API endpoint filter that appends X-Response-Time-ms header.
/// HIPAA §164.312(b) — Performance tracking for audit trail completeness.
/// </summary>
public class TrackPerformanceFilter(ILogger<TrackPerformanceFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var stopWatch = Stopwatch.StartNew();

        context.HttpContext.Response.OnStarting(() =>
        {
            stopWatch.Stop();
            context.HttpContext.Response.Headers.Append(
                "X-Response-Time-ms", $"{stopWatch.ElapsedMilliseconds}ms");
            return Task.CompletedTask;
        });

        var result = await next(context);

        logger.LogDebug("Endpoint executed in {ElapsedMs}ms", stopWatch.ElapsedMilliseconds);
        return result;
    }
}