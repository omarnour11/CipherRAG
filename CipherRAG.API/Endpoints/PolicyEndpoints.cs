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
                       .RequireAuthorization("AdminPolicy");

        group.MapGet("/", GetAllPolicies)
             .WithName("GetAllPolicies");

        group.MapGet("/roles", GetPolicyRoles)
             .WithName("GetPolicyRoles");

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

    public static async Task<IResult> GetPolicyRoles(AppDbContext context, CancellationToken ct)
    {
        var roles = await context.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleName)
            .Select(r => new RoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                RoleCategory = r.RoleCategory,
                ClinicalClearance = r.ClinicalClearance,
                CanManagePolicies = r.CanManagePolicies,
                CanViewAuditDashboard = r.CanViewAuditDashboard
            })
            .ToListAsync(ct);

        return Results.Ok(roles);
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
        if (request.RoleId == Guid.Empty)
        {
            return Results.BadRequest(new { Message = "Please select a valid target role." });
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

        var ips = request.AllowedIpRanges ?? [];
        if (ips.Count == 0) ips.Add("*");

        var countries = request.AllowedCountries ?? [];
        if (countries.Count == 0) countries.Add("*");

        var policy = new AccessPolicy
        {
            PolicyId = Guid.NewGuid(),
            RoleId = request.RoleId,
            Role = role,
            AllowedDepartment = string.IsNullOrWhiteSpace(request.AllowedDepartment) ? "*" : request.AllowedDepartment.Trim(),
            AllowedIpRanges = ips,
            AllowedCountries = countries,
            TimeWindow = timeWindow,
            IsActive = request.IsActive
        };

        context.AccessPolicies.Add(policy);
        await context.SaveChangesAsync(ct);

        return Results.Created($"/api/policies/{policy.PolicyId}", AccessPolicyDto.FromEntity(policy));
    }

    public static async Task<IResult> UpdatePolicy(Guid id, UpsertAccessPolicyRequest request, AppDbContext context, CancellationToken ct)
    {
        if (request.RoleId == Guid.Empty)
        {
            return Results.BadRequest(new { Message = "Please select a valid target role." });
        }

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

        var ips = request.AllowedIpRanges ?? [];
        if (ips.Count == 0) ips.Add("*");

        var countries = request.AllowedCountries ?? [];
        if (countries.Count == 0) countries.Add("*");

        policy.RoleId = request.RoleId;
        policy.Role = role;
        policy.AllowedDepartment = string.IsNullOrWhiteSpace(request.AllowedDepartment) ? "*" : request.AllowedDepartment.Trim();
        policy.AllowedIpRanges = ips;
        policy.AllowedCountries = countries;
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
            error = "Shift StartTime and EndTime must both be valid times (e.g. 08:00 to 17:00), or both left blank for 24/7 unrestricted access.";
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
        var policy = await context.AccessPolicies.FirstOrDefaultAsync(p => p.PolicyId == id, ct);
        if (policy is null)
        {
            return Results.NotFound(new { Message = $"Access policy with ID {id} was not found." });
        }

        try
        {
            context.AccessPolicies.Remove(policy);
            await context.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Policy successfully deleted.", policyId = id });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { Message = $"Could not delete policy: {ex.Message}" });
        }
    }
}
