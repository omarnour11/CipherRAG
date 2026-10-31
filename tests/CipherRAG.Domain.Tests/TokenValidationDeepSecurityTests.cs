using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class TokenValidationDeepSecurityTests
{
    private const string SecretKey = "CipherRAG_Default_Secure_Key_For_Cloud_Deployment_Min_32_Chars_2026!";
    private const string ValidIssuer = "CipherRAG";
    private const string ValidAudience = "CipherRAGUsers";

    private string CreateToken(
        string userId,
        string email,
        string role,
        string department,
        int clearanceLevel,
        TimeSpan lifetime,
        string? customSecretKey = null,
        string? issuer = null,
        string? audience = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(customSecretKey ?? SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

        var claims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role),
            new("role", role),
            new("Department", department),
            new("ClearanceLevel", clearanceLevel.ToString()),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expires = DateTime.UtcNow.Add(lifetime);
        var notBefore = lifetime < TimeSpan.Zero ? expires.AddMinutes(-10) : DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Audience = audience ?? ValidAudience,
            Issuer = issuer ?? ValidIssuer,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = creds
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    [Theory]
    // Roles Matrix
    [InlineData("SystemAdmin", "Cardiology", 5)]
    [InlineData("Physician", "Cardiology", 3)]
    [InlineData("ClinicalNurse", "Pediatrics", 2)]
    [InlineData("AuditOfficer", "Compliance", 4)]
    [InlineData("ComplianceAuditor", "Compliance", 4)]
    [InlineData("OperationsStaff", "Administration", 1)]
    [InlineData("BillingSpecialist", "Finance", 1)]
    [InlineData("Guest", "Public", 0)]
    [InlineData("Radiologist", "Radiology", 3)]
    [InlineData("Oncologist", "Oncology", 4)]
    [InlineData("CardiothoracicSurgeon", "Cardiology", 5)]
    [InlineData("PediatricNeurologist", "Pediatrics", 4)]
    [InlineData("EmergencyPhysician", "Emergency", 4)]
    [InlineData("Pharmacist", "Pharmacy", 2)]
    [InlineData("Pathologist", "Pathology", 3)]
    [InlineData("Intensivist", "IntensiveCare", 5)]
    [InlineData("MedicalResident", "InternalMedicine", 2)]
    [InlineData("ClinicalIntern", "Surgery", 1)]
    [InlineData("SecurityOfficer", "Security", 5)]
    [InlineData("ComplianceOfficer", "Compliance", 4)]
    [InlineData("MedicalRecordsClerk", "Records", 1)]
    [InlineData("ExecutiveDirector", "Administration", 5)]
    [InlineData("HeadNurse", "Nursing", 3)]
    [InlineData("TriageNurse", "Emergency", 2)]
    [InlineData("ResearchScientist", "Research", 3)]
    [InlineData("Geneticist", "Genetics", 4)]
    [InlineData("Anesthesiologist", "Anesthesiology", 5)]
    [InlineData("Psychiatrist", "Psychiatry", 3)]
    [InlineData("OrthopedicSurgeon", "Orthopedics", 4)]
    [InlineData("Dermatologist", "Dermatology", 3)]
    [InlineData("Ophthalmologist", "Ophthalmology", 3)]
    [InlineData("Urologist", "Urology", 4)]
    [InlineData("Endocrinologist", "Endocrinology", 3)]
    [InlineData("Gastroenterologist", "Gastroenterology", 4)]
    [InlineData("Nephrologist", "Nephrology", 4)]
    public async Task ValidToken_WithAllRoles_PassesValidation(string role, string dept, int clearance)
    {
        var userId = Guid.NewGuid().ToString();
        var email = $"{role.ToLower()}@hospital.local";
        var token = CreateToken(userId, email, role, dept, clearance, TimeSpan.FromMinutes(60));

        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ValidIssuer,
            ValidateAudience = true,
            ValidAudience = ValidAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
            RoleClaimType = ClaimTypes.Role
        };

        var result = await handler.ValidateTokenAsync(token, parameters);

        Assert.True(result.IsValid, result.Exception?.ToString());
        Assert.NotNull(result.ClaimsIdentity);

        // Verify sub is parsed as a single string (NOT an array, fixing IDX14101)
        var subClaim = result.ClaimsIdentity.FindFirst("sub");
        Assert.NotNull(subClaim);
        Assert.Equal(userId, subClaim.Value);

        // Verify role claim
        var roleClaim = result.ClaimsIdentity.FindFirst(ClaimTypes.Role) ?? result.ClaimsIdentity.FindFirst("role");
        Assert.NotNull(roleClaim);
        Assert.Equal(role, roleClaim.Value);

        // Verify department claim
        var deptClaim = result.ClaimsIdentity.FindFirst("Department");
        Assert.NotNull(deptClaim);
        Assert.Equal(dept, deptClaim.Value);

        // Verify clearance level claim
        var clearanceClaim = result.ClaimsIdentity.FindFirst("ClearanceLevel");
        Assert.NotNull(clearanceClaim);
        Assert.Equal(clearance.ToString(), clearanceClaim.Value);
    }

    [Theory]
    // Expired tokens
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-60)]
    [InlineData(-1440)]
    public async Task ExpiredToken_FailsValidation(int offsetMinutes)
    {
        var token = CreateToken(
            Guid.NewGuid().ToString(),
            "expired@cipherrag.local",
            "Physician",
            "Cardiology",
            2,
            TimeSpan.FromMinutes(offsetMinutes));

        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ValidIssuer,
            ValidateAudience = true,
            ValidAudience = ValidAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey))
        };

        var result = await handler.ValidateTokenAsync(token, parameters);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Exception);
    }

    [Theory]
    // Invalid / Malformed / Tampered Token Strings
    [InlineData("undefined")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bearer undefined")]
    [InlineData("Bearer null")]
    [InlineData("Bearer ")]
    [InlineData("not.a.jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")] // Only header
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0")] // No signature
    [InlineData("totally_invalid_random_string_that_is_not_jwt")]
    public async Task MalformedTokens_AreRejectedInstantly(string malformedToken)
    {
        var cleanToken = malformedToken.StartsWith("Bearer ") ? malformedToken[7..] : malformedToken;
        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ValidIssuer,
            ValidateAudience = true,
            ValidAudience = ValidAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey))
        };

        var result = await handler.ValidateTokenAsync(cleanToken, parameters);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TamperedTokenSignature_FailsValidation()
    {
        var token = CreateToken(
            Guid.NewGuid().ToString(),
            "tampered@cipherrag.local",
            "SystemAdmin",
            "IT",
            5,
            TimeSpan.FromMinutes(60));

        // Tamper with the signature portion (last part after last dot)
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        // Corrupt signature
        char lastChar = parts[2][^1];
        char corruptedChar = lastChar == 'A' ? 'B' : 'A';
        string tamperedToken = $"{parts[0]}.{parts[1]}.{parts[2][..^1]}{corruptedChar}";

        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ValidIssuer,
            ValidateAudience = true,
            ValidAudience = ValidAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey))
        };

        var result = await handler.ValidateTokenAsync(tamperedToken, parameters);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task WrongSigningKey_FailsValidation()
    {
        const string wrongKey = "Different_Key_That_Does_Not_Match_The_Configured_Secret_32Chars!";
        var token = CreateToken(
            Guid.NewGuid().ToString(),
            "wrongkey@cipherrag.local",
            "Physician",
            "Cardiology",
            3,
            TimeSpan.FromMinutes(60),
            customSecretKey: wrongKey);

        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ValidIssuer,
            ValidateAudience = true,
            ValidAudience = ValidAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey))
        };

        var result = await handler.ValidateTokenAsync(token, parameters);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("UntrustedIssuer", ValidAudience)]
    [InlineData(ValidIssuer, "UntrustedAudience")]
    [InlineData("UntrustedIssuer", "UntrustedAudience")]
    public async Task MismatchedIssuerOrAudience_FailsValidation(string issuer, string audience)
    {
        var token = CreateToken(
            Guid.NewGuid().ToString(),
            "mismatch@cipherrag.local",
            "Physician",
            "Cardiology",
            3,
            TimeSpan.FromMinutes(60),
            issuer: issuer,
            audience: audience);

        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ValidIssuer,
            ValidateAudience = true,
            ValidAudience = ValidAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey))
        };

        var result = await handler.ValidateTokenAsync(token, parameters);
        Assert.False(result.IsValid);
    }
}
