using CipherRAG.Application.DTOs;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.ValueObjects;
using CipherRAG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CipherRAG.Endpoints;

public static class PolicyEndpoints
{
    public static void MapPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies")
                       .WithTags("AccessPolicies")
                       .RequireAuthorization();

        group.MapGet("/", GetAllPolicies)
             .WithName("GetAllPolicies");


        group.MapGet("/{id:guid}", GetPolicyById)
             .WithName("GetPolicyById");

        group.MapPost("/", CreatePolicy)
             .WithName("CreatePolicy")
             .RequireAuthorization("AdminPolicy");

        group.MapPut("/{id:guid}", UpdatePolicy)
             .WithName("UpdatePolicy")
             .RequireAuthorization("AdminPolicy");

        group.MapPatch("/{id:guid}/toggle", TogglePolicy)
             .WithName("TogglePolicy")
             .RequireAuthorization("AdminPolicy");

        group.MapDelete("/{id:guid}", DeletePolicy)
             .WithName("DeletePolicy")
             .RequireAuthorization("AdminPolicy");
    }

    public static async Task<IResult> GetAllPolicies(AppDbContext context, CancellationToken ct)
    {
        var policies = await context.AccessPolicies
            .Include(p => p.Role)
            .OrderBy(p => p.Role!.RoleName)
            .ToListAsync(ct);

        return Results.Ok(policies.Select(AccessPolicyDto.FromEntity).ToList());
    }

    public static async Task<IResult> GetPolicyById(Guid id, AppDbContext context, CancellationToken ct)
    {
        var policy = await context.AccessPolicies
            .Include(p => p.Role)
            .FirstOrDefaultAsync(p => p.PolicyId == id, ct);

        return policy is null
            ? Results.NotFound(new { Message = $"Access policy with ID {id} was not found." })
            : Results.Ok(AccessPolicyDto.FromEntity(policy));
    }

    public static async Task<IResult> CreatePolicy(UpsertAccessPolicyRequest request, AppDbContext context, CancellationToken ct)
    {
        var role = await context.Roles.FindAsync(new object[] { request.RoleId }, ct);
        if (role is null)
        {
            return Results.BadRequest(new { Message = $"Role with ID {request.RoleId} does not exist." });
        }

        if (!TryParseTimeWindow(request, out var timeWindow, out var timeError))
        {
            return Results.BadRequest(new { Message = timeError });
        }

        var policy = new AccessPolicy
        {
            PolicyId = Guid.NewGuid(),
            RoleId = request.RoleId,
            AllowedDepartment = request.AllowedDepartment?.Trim() ?? string.Empty,
            AllowedIpRanges = request.AllowedIpRanges ?? [],
            AllowedCountries = request.AllowedCountries ?? [],
            TimeWindow = timeWindow,
            IsActive = request.IsActive
        };

        context.AccessPolicies.Add(policy);
        await context.SaveChangesAsync(ct);

        policy.Role = role;
        return Results.Created($"/api/policies/{policy.PolicyId}", AccessPolicyDto.FromEntity(policy));
    }

    public static async Task<IResult> UpdatePolicy(Guid id, UpsertAccessPolicyRequest request, AppDbContext context, CancellationToken ct)
    {
        var policy = await context.AccessPolicies
            .Include(p => p.Role)
            .FirstOrDefaultAsync(p => p.PolicyId == id, ct);

        if (policy is null)
        {
            return Results.NotFound(new { Message = $"Access policy with ID {id} was not found." });
        }

        var role = await context.Roles.FindAsync(new object[] { request.RoleId }, ct);
        if (role is null)
        {
            return Results.BadRequest(new { Message = $"Role with ID {request.RoleId} does not exist." });
        }

        if (!TryParseTimeWindow(request, out var timeWindow, out var timeError))
        {
            return Results.BadRequest(new { Message = timeError });
        }

        policy.RoleId = request.RoleId;
        policy.AllowedDepartment = request.AllowedDepartment?.Trim() ?? string.Empty;
        policy.AllowedIpRanges = request.AllowedIpRanges ?? [];
        policy.AllowedCountries = request.AllowedCountries ?? [];
        policy.TimeWindow = timeWindow;
        policy.IsActive = request.IsActive;

        await context.SaveChangesAsync(ct);
        return Results.Ok(AccessPolicyDto.FromEntity(policy));
    }

    private static bool TryParseTimeWindow(
        UpsertAccessPolicyRequest request,
        out TimeRange? timeWindow,
        out string? error)
    {
        timeWindow = null;
        error = null;

        var hasStart = !string.IsNullOrWhiteSpace(request.StartTime);
        var hasEnd = !string.IsNullOrWhiteSpace(request.EndTime);
        if (!hasStart && !hasEnd)
        {
            return true;
        }

        if (!hasStart || !hasEnd ||
            !TimeSpan.TryParse(request.StartTime, out var start) ||
            !TimeSpan.TryParse(request.EndTime, out var end) ||
            start < TimeSpan.Zero || start >= TimeSpan.FromDays(1) ||
            end < TimeSpan.Zero || end >= TimeSpan.FromDays(1))
        {
            error = "StartTime and EndTime must both be valid times in the range 00:00 through 23:59.";
            return false;
        }

        timeWindow = new TimeRange { StartTime = start, EndTime = end };
        return true;
    }

    public static async Task<IResult> TogglePolicy(Guid id, AppDbContext context, CancellationToken ct)
    {
        var policy = await context.AccessPolicies
            .Include(p => p.Role)
            .FirstOrDefaultAsync(p => p.PolicyId == id, ct);

        if (policy is null)
        {
            return Results.NotFound(new { Message = $"Access policy with ID {id} was not found." });
        }

        policy.IsActive = !policy.IsActive;
        await context.SaveChangesAsync(ct);
        return Results.Ok(new { message = $"Policy active state changed to {policy.IsActive}.", isActive = policy.IsActive });
    }

    public static async Task<IResult> DeletePolicy(Guid id, AppDbContext context, CancellationToken ct)
    {
        var policy = await context.AccessPolicies.FindAsync(new object[] { id }, ct);
        if (policy is null)
        {
            return Results.NotFound(new { Message = $"Access policy with ID {id} was not found." });
        }

        context.AccessPolicies.Remove(policy);
        await context.SaveChangesAsync(ct);
        return Results.Ok(new { message = "Policy successfully deleted." });
    }
}
