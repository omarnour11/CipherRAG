using System.Security.Claims;
using CipherRAG.API.Filters;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CipherRAG.Endpoints;

public static class RagEndpoints
{
    public static void MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rag")
                       .WithTags("RAG & Chat")
                       .AddEndpointFilter<TrackPerformanceFilter>()
                       .RequireAuthorization();

        group.MapPost("/ask", AskQuestion)
             .WithName("AskRagQuestion")
             .WithSummary("Process user query through security and RAG retrieval pipeline")
             .RequireRateLimiting("RagPolicy");

        group.MapPost("/query", AskQuestion)
             .WithName("QueryRagQuestion")
             .WithSummary("Alias for RAG query processing")
             .RequireRateLimiting("RagPolicy");
    }

    public static async Task<IResult> AskQuestion(
        RagAskRequest request,
        HttpContext httpContext,
        IRagService ragService,
        ILogger<RagAskRequest> logger,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            logger?.LogWarning("AskQuestion called with null or empty UserPrompt");
            return Results.BadRequest(new { Message = "UserPrompt is required." });
        }

        // Prioritize logged-in user identity from session claims
        var claimUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid finalUserId = Guid.Empty;

        if (Guid.TryParse(claimUserId, out var parsedClaimId) && parsedClaimId != Guid.Empty)
        {
            finalUserId = parsedClaimId;
        }
        else if (request.UserId != Guid.Empty)
        {
            finalUserId = request.UserId;
        }

        var finalDepartment = string.IsNullOrWhiteSpace(request.Department)
            ? (httpContext.User.FindFirstValue("Department") ?? "CyberSecurity")
            : request.Department;

        // HIPAA §164.312(a)(1) — Access control: session claim is authoritative.
        // If user submitted an explicit clearance, cap it to authenticated claim if present.
        var clearanceClaim = httpContext.User.FindFirstValue("ClearanceLevel");
        int finalClearance = request.ClearanceLevel;
        if (int.TryParse(clearanceClaim, out var parsedClearance) && parsedClearance > 0)
        {
            finalClearance = request.ClearanceLevel > 0
                ? Math.Min(request.ClearanceLevel, parsedClearance)
                : parsedClearance;
        }

        logger?.LogInformation("RAG Ask request prepared for UserId: {UserId}, Department: {Department}, Clearance: {Clearance}", finalUserId, finalDepartment, finalClearance);

        var enrichedRequest = request with
        {
            UserId = finalUserId,
            Department = finalDepartment,
            ClearanceLevel = finalClearance
        };

            logger?.LogDebug("Invoking IRagService.AskAsync with prompt length: {Length} characters", enrichedRequest.UserPrompt?.Length ?? 0);     
            var response = await ragService.AskAsync(enrichedRequest, ct);
            logger?.LogInformation("RAG response received for UserId: {UserId}", finalUserId);
        return Results.Ok(response);
    }
}
