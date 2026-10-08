using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CipherRAG.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health")
                       .WithTags("System");

        group.MapGet("/", GetHealthStatus)
             .WithName("GetHealthStatus");
    }

    public static IResult GetHealthStatus(ILogger<Program> logger)
    {
        logger.LogInformation("Health check requested.");

        return Results.Ok(new
        {
            Status = "Healthy",
            System = "CipherRAG API",
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
