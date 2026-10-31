using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class TokenValidationTests
{
    private const string SecretKey = "CipherRAG_Default_Secure_Key_For_Cloud_Deployment_Min_32_Chars_2026!";

    [Fact]
    public async Task GeneratedToken_CanBeValidatedBy_JsonWebTokenHandler()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

        var userId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, "test@cipherrag.local"),
            new(ClaimTypes.Role, "SystemAdmin"),
            new("role", "SystemAdmin"),
            new("ClearanceLevel", "3"),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Audience = "CipherRAGUsers",
            Issuer = "CipherRAG",
            Expires = DateTime.UtcNow.AddMinutes(60),
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(descriptor);
        var tokenString = tokenHandler.WriteToken(securityToken);

        // Validate using JsonWebTokenHandler (used by ASP.NET Core)
        var jsonHandler = new JsonWebTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "CipherRAG",
            ValidateAudience = true,
            ValidAudience = "CipherRAGUsers",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            RoleClaimType = ClaimTypes.Role
        };

        var result = await jsonHandler.ValidateTokenAsync(tokenString, validationParameters);

        Assert.True(result.IsValid, result.Exception?.ToString());
        Assert.NotNull(result.ClaimsIdentity);

        // Sub claim is parsed as string (not array)
        var sub = result.ClaimsIdentity.FindFirst("sub");
        Assert.NotNull(sub);
        Assert.Equal(userId, sub.Value);
    }

    [Fact]
    public async Task MalformedOrUndefinedToken_FailsValidation()
    {
        var jsonHandler = new JsonWebTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "CipherRAG",
            ValidateAudience = true,
            ValidAudience = "CipherRAGUsers",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key
        };

        var result = await jsonHandler.ValidateTokenAsync("undefined", validationParameters);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Exception);
    }
}
