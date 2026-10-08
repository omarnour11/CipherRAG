using System.Security.Claims;
using CipherRAG.API.Filters;
using CipherRAG.Application.Helpers;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CipherRAG.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authApi = app.MapGroup("/api/auth")
                         .WithTags("Authentication")
                         .AddEndpointFilter<TrackPerformanceFilter>();

        authApi.MapPost("/login", Login)
               .WithName("Login")
               .WithSummary("Authenticate user and return a JWT")
               .RequireRateLimiting("AuthPolicy")
               .AllowAnonymous();

        authApi.MapPost("/logout", Logout)
               .WithName("Logout")
               .WithSummary("Invalidate the current session")
               .RequireAuthorization();

        authApi.MapGet("/me", GetCurrentUser)
               .WithName("GetCurrentUser")
               .WithSummary("Get authenticated user claims details")
               .RequireAuthorization();
    }

    public static async Task<IResult> Login(
        IPasswordHasher hasher,
        ITokenProvider tokenProvider,
        LoginRequest request,
        IUnitOfWork unitOfWork,
        ILogger<Program> logger)
    {
        var user = await unitOfWork.Users.GetByEmailAsync(request.Email);

        if (user is null)
        {
            logger.LogWarning("Unauthorized login attempt for email: {Email}", EmailMasker.Mask(request.Email));
            return Results.Unauthorized();
        }

        var isValidPassword = hasher.Verify(request.Password, user.PasswordHash);

        if (!isValidPassword)
        {
            logger.LogWarning("Wrong password for email: {Email}", EmailMasker.Mask(request.Email));
            return Results.Unauthorized();
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Inactive account login attempt for email: {Email}", EmailMasker.Mask(request.Email));
            return Results.Json(new { message = "User account is deactivated." }, statusCode: 403);
        }

        var userWithRole = await unitOfWork.Users.GetUserWithRolesAsync(user.UserId);
        var roleName = userWithRole?.Role?.RoleName ?? "User";
        var clearanceLevel = userWithRole?.Role?.ClinicalClearance ?? 1;

        var tokenResponse = await tokenProvider.GenerateJwtTokenAsync(new UserDto
        {
            UserId = user.UserId,
            RoleId = user.RoleId,
            FullName = user.FullName,
            Email = user.Email,
            Department = user.Department,
            IsActive = user.IsActive,
            RoleName = roleName,
            ClearanceLevel = clearanceLevel
        });

        logger.LogInformation("JWT issued successfully for user ID: {UserId}", user.UserId);

        return Results.Ok(new
        {
            message = "Login successful",
            userId = user.UserId,
            email = user.Email,
            role = roleName,
            accessToken = tokenResponse.AccessToken,
            refreshToken = tokenResponse.RefreshToken,
            expiresOnUtc = tokenResponse.ExpiresOnUtc
        });
    }

    public static async Task<IResult> Logout(
        HttpContext httpContext,
        ITokenProvider tokenProvider,
        CancellationToken ct)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Results.Unauthorized();
        }

        await tokenProvider.RevokeRefreshTokensAsync(userId, ct);
        return TypedResults.NoContent();
    }

    public static async Task<IResult> GetCurrentUser(HttpContext httpContext, IUnitOfWork unitOfWork)
    {
        var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var user = await unitOfWork.Users.GetUserWithRolesAsync(userId);
        if (user is null)
            return Results.NotFound(new { Message = "Authenticated user profile not found." });

        return Results.Ok(UserDto.FromEntity(user));
    }
}