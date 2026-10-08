using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace CipherRAG.Application.Filters;



public class TrackPerformanceFilter : IEndpointFilter

{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
var stopWatch = Stopwatch.StartNew();

        Console.WriteLine("Performance Filter is executing...");

        context.HttpContext.Response.OnStarting(() =>
        {
            stopWatch.Stop();
            var elapsedMs = stopWatch.ElapsedMilliseconds;

            context.HttpContext.Response.Headers.Append("X-Response-Time-ms", $"{elapsedMs}ms");

            return Task.CompletedTask;
        });

        var result = await next(context);

        Console.WriteLine($"It took {stopWatch.ElapsedMilliseconds}ms");

        return result;    
    }
}