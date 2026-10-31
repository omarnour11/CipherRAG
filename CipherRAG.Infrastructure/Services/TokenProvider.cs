using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CipherRAG.Application.DTOs;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;

namespace CipherRAG.Infrastructure.Services;

public class TokenProvider(IConfiguration configuration, IAppDbContext context) : ITokenProvider 
{   
    private readonly IConfiguration _configuration = configuration;
    private readonly IAppDbContext _context = context;

    public async Task<TokenResponse> GenerateJwtTokenAsync(UserDto user, CancellationToken ct = default)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var issuer = jwtSettings["Issuer"] ?? "CipherRAG";
        var audience = jwtSettings["Audience"] ?? "CipherRAGUsers";
        var key = jwtSettings["Key"];

        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            key = "CipherRAG_Default_Secure_Key_For_Cloud_Deployment_Min_32_Chars_2026!";
        }

        var expirationMinutesStr = jwtSettings["TokenExpirationInMinutes"];
        var expirationMinutes = int.TryParse(expirationMinutesStr, out var m) ? m : 60;
        var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new("sub", user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, user.RoleName),
            new("role", user.RoleName),
            new("Department", user.Department ?? string.Empty),
            new("ClearanceLevel", user.ClearanceLevel.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Audience = audience,
            Issuer = issuer,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var securityToken = tokenHandler.CreateToken(descriptor);

        await _context.RefreshTokens
            .Where(rt => rt.UserId == user.UserId)
            .ExecuteDeleteAsync(ct);

        var rawRefreshToken = GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            user.UserId,
            rawRefreshToken,
            DateTimeOffset.UtcNow.AddDays(7),
            devicePlatform: "Web",
            deviceId: $"web-{Guid.NewGuid():N}");

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = tokenHandler.WriteToken(securityToken),
            RefreshToken = rawRefreshToken,
            ExpiresOnUtc = expires
        };
    }

    public async Task RevokeRefreshTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _context.RefreshTokens
            .Where(token => token.UserId == userId && token.IsRevoked == false)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        if (tokens.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
