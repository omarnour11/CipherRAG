using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using CipherRAG.Application.DTOs;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using CipherRAG.Infrastructure.Persistence;

namespace CipherRAG.Endpoints;

public static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAuthorization("AdminPolicy");

        group.MapGet("/", GetRoles).WithName("GetRoles");
        group.MapGet("/{id:guid}", GetRole).WithName("GetRole");
        group.MapPost("/", CreateRole).WithName("CreateRole");
        group.MapPut("/{id:guid}", UpdateRole).WithName("UpdateRole");
        group.MapDelete("/{id:guid}", DeleteRole).WithName("DeleteRole");
    }

    public static async Task<IResult> GetRoles(AppDbContext context, CancellationToken ct)
    {
        var roles = await context.Roles
            .AsNoTracking()
            .OrderBy(role => role.RoleName)
            .Select(ToRoleDto)
            .ToListAsync(ct);

        return Results.Ok(roles);
    }

    public static async Task<IResult> GetRole(Guid id, AppDbContext context, CancellationToken ct)
    {
        var role = await context.Roles
            .AsNoTracking()
            .Where(item => item.RoleId == id)
            .Select(ToRoleDto)
            .FirstOrDefaultAsync(ct);

        return role is null
            ? Results.NotFound(new { Message = $"Role with ID {id} was not found." })
            : Results.Ok(role);
    }

    public static async Task<IResult> CreateRole(UpsertRoleRequest request, AppDbContext context, CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null) return validation;

        var roleName = request.RoleName.Trim();
        if (await NameExists(context, roleName, null, ct))
            return Results.Conflict(new { Message = $"Role '{roleName}' already exists." });

        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            RoleName = roleName,
            RoleCategory = request.RoleCategory,
            ClinicalClearance = request.ClinicalClearance,
            CanManagePolicies = request.CanManagePolicies,
            CanViewAuditDashboard = request.CanViewAuditDashboard
        };

        context.Roles.Add(role);
        await context.SaveChangesAsync(ct);
        return Results.Created($"/api/roles/{role.RoleId}", ToRoleDto.Compile()(role));
    }

    public static async Task<IResult> UpdateRole(Guid id, UpsertRoleRequest request, AppDbContext context, CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null) return validation;

        var role = await context.Roles.FindAsync([id], ct);
        if (role is null)
            return Results.NotFound(new { Message = $"Role with ID {id} was not found." });

        var roleName = request.RoleName.Trim();
        if (await NameExists(context, roleName, id, ct))
            return Results.Conflict(new { Message = $"Role '{roleName}' already exists." });

        role.RoleName = roleName;
        role.RoleCategory = request.RoleCategory;
        role.ClinicalClearance = request.ClinicalClearance;
        role.CanManagePolicies = request.CanManagePolicies;
        role.CanViewAuditDashboard = request.CanViewAuditDashboard;
        await context.SaveChangesAsync(ct);

        return Results.Ok(ToRoleDto.Compile()(role));
    }

    public static async Task<IResult> DeleteRole(Guid id, AppDbContext context, CancellationToken ct)
    {
        var role = await context.Roles
            .Where(item => item.RoleId == id)
            .Select(item => new
            {
                Role = item,
                UserCount = item.Users.Count,
                PolicyCount = item.AccessPolicies.Count
            })
            .FirstOrDefaultAsync(ct);

        if (role is null)
            return Results.NotFound(new { Message = $"Role with ID {id} was not found." });

        if (role.UserCount > 0 || role.PolicyCount > 0)
        {
            return Results.Conflict(new
            {
                Message = "Role cannot be deleted while users or access policies are assigned to it.",
                role.UserCount,
                role.PolicyCount
            });
        }

        context.Roles.Remove(role.Role);
        await context.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static IResult? ValidateRequest(UpsertRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName) || request.RoleName.Trim().Length > 255)
            return Results.BadRequest(new { Message = "Role name is required and must be at most 255 characters." });

        if (!Enum.IsDefined(request.RoleCategory))
            return Results.BadRequest(new { Message = "Role category is invalid." });

        if (request.ClinicalClearance is < 0 or > 5)
            return Results.BadRequest(new { Message = "Clinical clearance must be between 0 and 5." });

        return null;
    }

    private static Task<bool> NameExists(AppDbContext context, string name, Guid? excludedId, CancellationToken ct)
    {
        var normalizedName = name.ToLower();
        return context.Roles.AnyAsync(role =>
            role.RoleName.ToLower() == normalizedName &&
            (!excludedId.HasValue || role.RoleId != excludedId.Value), ct);
    }

    private static readonly Expression<Func<Role, RoleDto>> ToRoleDto = role => new RoleDto
    {
        RoleId = role.RoleId,
        RoleName = role.RoleName,
        RoleCategory = role.RoleCategory,
        ClinicalClearance = role.ClinicalClearance,
        CanManagePolicies = role.CanManagePolicies,
        CanViewAuditDashboard = role.CanViewAuditDashboard,
        UserCount = role.Users.Count,
        PolicyCount = role.AccessPolicies.Count
    };
}
