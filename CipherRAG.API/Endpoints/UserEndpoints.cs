using CipherRAG.API.Filters;
using CipherRAG.Application.Helpers;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;
using CipherRAG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CipherRAG.Endpoints;

public static class UserEndpoints 
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
                       .WithTags("Users")
                       .AddEndpointFilter<TrackPerformanceFilter>()
                       .RequireAuthorization();

        group.MapGet("/", GetAllUsers)
             .WithName("GetAllUsers")
             .RequireAuthorization("AdminPolicy")
             .RequireRateLimiting("DefaultPolicy");

        group.MapGet("/paged", GetPagedUsers)
             .WithName("GetPagedUsers")
             .RequireAuthorization("AdminPolicy");

        group.MapGet("/{id:guid}", GetUserById)
             .WithName("GetUserById")
             .RequireAuthorization();

        group.MapPost("/", CreateUser)
             .WithName("CreateUser")
             .RequireAuthorization("AdminPolicy");

        group.MapPut("/{id:guid}", UpdateUser)
             .WithName("UpdateUser")
             .RequireAuthorization("AdminPolicy");

        group.MapDelete("/{id:guid}", DeleteUser)
             .WithName("DeleteUser")
             .RequireAuthorization("AdminPolicy");
    }

    public static async Task<IResult> GetAllUsers(IUnitOfWork unitOfWork, ICacheService cacheService, CancellationToken ct)
    {
        var dtos = await cacheService.GetOrCreateAsync("users:all", async _ =>
        {
            var users = await unitOfWork.Users.GetAllWithRolesAsync();
            return users.Select(UserDto.FromEntity).ToList();
        }, TimeSpan.FromMinutes(5), ct);

        return Results.Ok(dtos);
    }

    public static async Task<IResult> GetPagedUsers(int pageNumber, int pageSize, IUnitOfWork unitOfWork, ICacheService cacheService, CancellationToken ct)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 15;

        var response = await cacheService.GetOrCreateAsync(
            $"users:paged:{pageNumber}:{pageSize}", async _ =>
            {
                var pagedUsers = await unitOfWork.Users.GetAllWithRolesAsyncPaged(pageNumber, pageSize);
                var dtos = pagedUsers.Items.Select(UserDto.FromEntity);
                return new PagedResultResponse<UserDto>(
                    dtos, pagedUsers.TotalCount, pagedUsers.TotalPages, pagedUsers.PageNumber, pagedUsers.PageSize);
            }, TimeSpan.FromMinutes(2), ct);

        return Results.Ok(response);
    }

    public static async Task<IResult> GetUserById(Guid id, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("GetUserById called: id={UserId}", id);
        var dto = await cacheService.GetOrCreateAsync($"users:{id}", async _ =>
        {
            var user = await unitOfWork.Users.GetUserWithRolesAsync(id);
            return user is null ? null : UserDto.FromEntity(user);
        }, TimeSpan.FromMinutes(5), ct);

        if (dto is null)
        {
            logger.LogWarning("User not found: id={UserId}", id);
            return Results.NotFound(new { Message = $"User with ID {id} was not found." });
        }

        return Results.Ok(dto);
    }

    public static async Task<IResult> CreateUser(
        IPasswordHasher hasher, CreateUserRequest req, AppDbContext context, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("CreateUser called: email={Email}", req.Email);

        var normalizedEmail = req.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Results.BadRequest(new { Message = "Email address is required." });
        }

        var existing = await context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail.ToLower(), ct);
        if (existing)
        {
            logger.LogWarning("CreateUser failed - email exists: {Email}", EmailMasker.Mask(normalizedEmail));
            return Results.Conflict(new { Message = "User with this email already exists." });
        }

        var role = await context.Roles.FindAsync(new object[] { req.RoleId }, ct);
        if (role is null)
        {
            logger.LogWarning("CreateUser failed - invalid RoleId: {RoleId}", req.RoleId);
            return Results.BadRequest(new { Message = $"Role with ID '{req.RoleId}' does not exist." });
        }

        var passwordHash = hasher.Hash(req.Password);

        var user = new User
        {
            UserId = Guid.NewGuid(),
            RoleId = req.RoleId,
            Role = role,
            FullName = req.FullName?.Trim() ?? string.Empty,
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Department = string.IsNullOrWhiteSpace(req.Department) ? "General" : req.Department.Trim(),
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(ct);
        await cacheService.RemoveByPrefixAsync("users", ct);

        logger.LogInformation("User created: id={UserId}, email={Email}", user.UserId, EmailMasker.Mask(user.Email));
        return Results.Created($"/api/users/{user.UserId}", UserDto.FromEntity(user));
    }

    public static async Task<IResult> UpdateUser(
        Guid id, UpdateUserRequest req, AppDbContext context, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("UpdateUser called: id={UserId}", id);
        var user = await context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id, ct);
        if (user is null)
        {
            logger.LogWarning("UpdateUser failed - not found: id={UserId}", id);
            return Results.NotFound(new { Message = $"User with ID {id} was not found." });
        }

        var normalizedEmail = req.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Results.BadRequest(new { Message = "Email address cannot be empty." });
        }

        var duplicate = await context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail.ToLower() && u.UserId != id, ct);
        if (duplicate)
        {
            return Results.Conflict(new { Message = $"Another user is already registered with email '{normalizedEmail}'." });
        }

        var role = await context.Roles.FindAsync(new object[] { req.RoleId }, ct);
        if (role is null)
        {
            logger.LogWarning("UpdateUser failed - invalid RoleId: {RoleId}", req.RoleId);
            return Results.BadRequest(new { Message = $"Role with ID '{req.RoleId}' does not exist." });
        }

        user.RoleId = req.RoleId;
        user.Role = role;
        user.FullName = req.FullName?.Trim() ?? string.Empty;
        user.Email = normalizedEmail;
        user.Department = string.IsNullOrWhiteSpace(req.Department) ? "General" : req.Department.Trim();
        user.IsActive = req.IsActive;

        await context.SaveChangesAsync(ct);
        await cacheService.RemoveByPrefixAsync("users", ct);

        logger.LogInformation("User updated: id={UserId}", id);
        return Results.Ok(UserDto.FromEntity(user));
    }

    public static async Task<IResult> DeleteUser(
        Guid id, AppDbContext context, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("DeleteUser called: id={UserId}", id);
        var user = await context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id, ct);
        if (user is null)
        {
            logger.LogWarning("DeleteUser failed - not found: id={UserId}", id);
            return Results.NotFound(new { Message = $"User with ID {id} was not found." });
        }

        // Check if user has associated compliance/audit or document records
        bool hasAuditLogs = await context.AuditLogs.AnyAsync(a => a.UserId == id, ct);
        bool hasDocuments = await context.Documents.AnyAsync(d => d.UserId == id, ct);
        bool hasRequests = await context.RagRequests.AnyAsync(r => r.UserId == id, ct);

        if (hasAuditLogs || hasDocuments || hasRequests)
        {
            // HIPAA §164.312(b): Audit logs and clinical records are immutable.
            // Safely deactivate account and revoke all tokens to disable access without breaking referential integrity.
            user.IsActive = false;
            await context.RefreshTokens.Where(rt => rt.UserId == id).ExecuteDeleteAsync(ct);
            await context.SaveChangesAsync(ct);
            await cacheService.RemoveByPrefixAsync("users", ct);

            logger.LogInformation("User {UserId} deactivated because associated audit/clinical records exist", id);
            return Results.Ok(new 
            { 
                Message = $"User '{user.FullName}' cannot be permanently deleted because associated compliance/audit records exist. The account has been deactivated and access tokens revoked.",
                Deactivated = true 
            });
        }

        try
        {
            // Remove transient tokens and sessions first
            await context.RefreshTokens.Where(rt => rt.UserId == id).ExecuteDeleteAsync(ct);
            await context.MaskingSessions.Where(ms => ms.UserId == id).ExecuteDeleteAsync(ct);

            context.Users.Remove(user);
            await context.SaveChangesAsync(ct);
            await cacheService.RemoveByPrefixAsync("users", ct);

            logger.LogInformation("User permanently deleted: id={UserId}", id);
            return Results.Ok(new { Message = $"User '{user.FullName}' deleted successfully.", Deleted = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete user {UserId}", id);
            // Fallback to safe deactivation if any DB constraint was encountered
            user.IsActive = false;
            await context.SaveChangesAsync(ct);
            await cacheService.RemoveByPrefixAsync("users", ct);
            return Results.Ok(new 
            { 
                Message = $"User '{user.FullName}' has been deactivated and credentials disabled.",
                Deactivated = true 
            });
        }
    }
}
