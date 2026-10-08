using CipherRAG.API.Filters;
using CipherRAG.Application.Helpers;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Metadata;

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

    // ==========================================
    // Handler Method Definitions
    // ==========================================

    public static async Task<IResult> GetAllUsers(IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("GetAllUsers called");
        var dtos = await cacheService.GetOrCreateAsync("users:all", async _ =>
        {
            var users = await unitOfWork.Users.GetAllWithRolesAsync();
            return users.Select(UserDto.FromEntity).ToList();
        }, TimeSpan.FromMinutes(5), ct);

        return Results.Ok(dtos);
    }

    public static async Task<IResult> GetPagedUsers(int pageNumber, int pageSize, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("GetPagedUsers called: pageNumber={PageNumber}, pageSize={PageSize}", pageNumber, pageSize);
        var response = await cacheService.GetOrCreateAsync($"users:paged:{pageNumber}:{pageSize}", async _ =>
        {
            var pagedUsers = await unitOfWork.Users.GetAllWithRolesAsyncPaged(pageNumber, pageSize);
            var dtos = pagedUsers.Items.Select(UserDto.FromEntity);
            return new PagedResultResponse<UserDto>(
                dtos, pagedUsers.TotalCount, pagedUsers.TotalPages, pagedUsers.PageNumber, pagedUsers.PageSize);
        }, TimeSpan.FromMinutes(5), ct);

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

    public static async Task<IResult> CreateUser(IPasswordHasher hasher , CreateUserRequest req, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("CreateUser called: email={Email}", req.Email);
        var existing = await unitOfWork.Users.GetByEmailAsync(req.Email);
        if (existing is not null)
        {
            logger.LogWarning("CreateUser failed - email exists: {Email}",EmailMasker.Mask(req.Email));
            return Results.BadRequest(new { Message = "User with this email already exists." });
        }

        var role = await unitOfWork.Roles.GetByIdAsync(req.RoleId);
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
            FullName = req.FullName,
            Email = req.Email,
            PasswordHash = passwordHash,
            Department = req.Department,
            IsActive = true
        };

        await unitOfWork.Users.AddAsync(user);
        await unitOfWork.CompleteAsync();

        await cacheService.RemoveByPrefixAsync("users", ct);

        logger.LogInformation("User created: id={UserId}, email={Email}", user.UserId, EmailMasker.Mask(user.Email));
        return Results.Created($"/api/users/{user.UserId}", UserDto.FromEntity(user));
    }

    public static async Task<IResult> UpdateUser(Guid id, UpdateUserRequest req, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("UpdateUser called: id={UserId}", id);
        var user = await unitOfWork.Users.GetByIdAsync(id);
        if (user is null)
        {
            logger.LogWarning("UpdateUser failed - not found: id={UserId}", id);
            return Results.NotFound(new { Message = $"User with ID {id} was not found." });
        }

        var role = await unitOfWork.Roles.GetByIdAsync(req.RoleId);
        if (role is null)
        {
            logger.LogWarning("UpdateUser failed - invalid RoleId: {RoleId}", req.RoleId);
            return Results.BadRequest(new { Message = $"Role with ID '{req.RoleId}' does not exist." });
        }

        user.RoleId = req.RoleId;
        user.FullName = req.FullName;
        user.Email = req.Email;
        user.Department = req.Department;
        user.IsActive = req.IsActive;

        unitOfWork.Users.Update(user);
        await unitOfWork.CompleteAsync();

        await cacheService.RemoveByPrefixAsync("users", ct);

        logger.LogInformation("User updated: id={UserId}", id);
        return Results.Ok(UserDto.FromEntity(user));
    }

    public static async Task<IResult> DeleteUser(Guid id, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<User> logger, CancellationToken ct)
    {
        logger.LogInformation("DeleteUser called: id={UserId}", id);
        var user = await unitOfWork.Users.GetByIdAsync(id);
        if (user is null)
        {
            logger.LogWarning("DeleteUser failed - not found: id={UserId}", id);
            return Results.NotFound(new { Message = $"User with ID {id} was not found." });
        }

        unitOfWork.Users.Delete(user);
        await unitOfWork.CompleteAsync();

        await cacheService.RemoveByPrefixAsync("users", ct);

        logger.LogInformation("User deleted: id={UserId}", id);
        return Results.NoContent();
    }
}
