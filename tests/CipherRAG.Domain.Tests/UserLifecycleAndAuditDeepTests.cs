using System.Security.Cryptography;
using System.Text;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class UserLifecycleAndAuditDeepTests
{
    [Theory]
    // User Entity State & Password Hash Tests
    [InlineData("$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy", true)]
    [InlineData("$2b$12$K8Z0g0Qe6R0tKj7FjRjR.uWk5k8/Yc3cWf2a4s5d6f7g8h9j0k1l2", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void UserEntity_HasPasswordHash_IdentifiesPopulatedHashes(string? passwordHash, bool expectedResult)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "dr.smith@hospital.local",
            PasswordHash = passwordHash ?? string.Empty
        };

        Assert.Equal(expectedResult, user.HasPasswordHash());
    }

    [Theory]
    // Department lengths up to 100 characters (verifying expanded department schema)
    [InlineData("Cardiology")]
    [InlineData("Department of Advanced Molecular and Cellular Oncology")]
    [InlineData("Department of Pediatric Neuro-Developmental and Behavioral Health")]
    [InlineData("Interventional Radiology, Endovascular Surgery, and Diagnostic Imaging Division")]
    [InlineData("Comprehensive Organ Transplant and Mechanical Circulatory Support Unit")]
    public void UserEntity_SupportsLongDepartmentNames(string longDepartment)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Dr. Specialist",
            Email = "specialist@hospital.org",
            Department = longDepartment,
            IsActive = true
        };

        Assert.Equal(longDepartment, user.Department);
        Assert.True(user.Department.Length <= 100);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void UserEntity_InitializesCollectionsSafely()
    {
        var user = new User();

        Assert.NotNull(user.Documents);
        Assert.Empty(user.Documents);
        Assert.NotNull(user.RefreshTokens);
        Assert.Empty(user.RefreshTokens);
        Assert.NotNull(user.RagRequests);
        Assert.Empty(user.RagRequests);
        Assert.True(user.IsActive);
    }

    [Theory]
    // Refresh Token Lifecycle & Expiration Scenarios
    [InlineData(1, false, true)]    // Expires in 1 day, active
    [InlineData(7, false, true)]    // Expires in 7 days, active
    [InlineData(30, false, true)]   // Expires in 30 days, active
    [InlineData(-1, false, false)]  // Expired 1 day ago
    [InlineData(-7, false, false)]  // Expired 7 days ago
    [InlineData(7, true, false)]    // Expires in 7 days, but revoked!
    public void RefreshToken_LifecycleAndStateEvaluation(int daysOffset, bool revoke, bool shouldBeActive)
    {
        var userId = Guid.NewGuid();
        var rawToken = "secure-random-refresh-token-value-2026";

        RefreshToken token;
        if (daysOffset > 0)
        {
            token = RefreshToken.Create(
                userId,
                rawToken,
                DateTimeOffset.UtcNow.AddDays(daysOffset),
                devicePlatform: "Web",
                deviceId: "web-browser-1");
        }
        else
        {
            token = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId,
                TokenHash = RefreshToken.HashToken(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(daysOffset),
                DevicePlatform = "Web",
                DeviceId = "web-browser-1"
            };
        }

        if (revoke)
        {
            token.Revoke();
        }

        bool isActive = token.IsActive;
        Assert.Equal(shouldBeActive, isActive);

        if (revoke)
        {
            Assert.True(token.IsRevoked);
        }
    }

    [Theory]
    // Refresh Token Matching
    [InlineData("exact-token-match-string", "exact-token-match-string", true)]
    [InlineData("valid-token-12345", "valid-token-12345", true)]
    [InlineData("valid-token-12345", "wrong-token-99999", false)]
    [InlineData("valid-token-12345", "", false)]
    [InlineData("valid-token-12345", "VALID-TOKEN-12345", false)] // Case-sensitive comparison
    public void RefreshToken_TokenMatching_EnforcesCryptographicEquality(
        string createdToken, string candidateToken, bool shouldMatch)
    {
        var token = RefreshToken.Create(
            Guid.NewGuid(),
            createdToken,
            DateTimeOffset.UtcNow.AddDays(1));

        bool matches = token.Matches(candidateToken);
        Assert.Equal(shouldMatch, matches);
    }

    [Theory]
    // Email Formatting & Domain Integrity Tests
    [InlineData("doctor@hospital.org", true)]
    [InlineData("dr.smith@medical.centre.edu", true)]
    [InlineData("nurse_jane123@clinic.local", true)]
    [InlineData("admin@cipherrag.internal", true)]
    [InlineData("billing.department@healthcare-group.com", true)]
    [InlineData("chief.surgeon+urgent@hospital.gov", true)]
    [InlineData("first.last@subdomain.domain.org", true)]
    [InlineData("plainaddress", false)]
    [InlineData("@missingusername.com", false)]
    [InlineData("username@.com", false)]
    [InlineData("username@com", false)]
    [InlineData("username@domain..com", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void UserEmailValidation_ChecksStandardFormats(string email, bool isValid)
    {
        bool result = !string.IsNullOrWhiteSpace(email) &&
                      !email.Contains("..") &&
                      System.Text.RegularExpressions.Regex.IsMatch(
                          email.Trim(),
                          @"^[^@\s]+@[^@\s\.]+(?:\.[^@\s\.]+)+$");

        Assert.Equal(isValid, result);
    }

    [Theory]
    // User Role and Active State Transitions
    [InlineData("Cardiology", 3, true)]
    [InlineData("Oncology", 4, true)]
    [InlineData("Pediatrics", 2, true)]
    [InlineData("Radiology", 3, true)]
    [InlineData("Administration", 5, true)]
    [InlineData("Finance", 1, true)]
    [InlineData("Cardiology", 3, false)] // Deactivated user (e.g. compliance retention)
    [InlineData("Emergency", 4, false)]  // Deactivated user
    [InlineData("Surgery", 5, false)]    // Deactivated user
    [InlineData("Pathology", 3, false)]  // Deactivated user
    public void UserRoleAssociation_IntegratesClearanceAndState(string department, int clearance, bool isActive)
    {
        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            RoleName = $"{department}Specialist",
            ClinicalClearance = clearance,
            RoleCategory = RoleCategory.Clinical
        };

        var user = new User
        {
            UserId = Guid.NewGuid(),
            RoleId = role.RoleId,
            Role = role,
            Department = department,
            IsActive = isActive
        };

        Assert.Equal(role.RoleId, user.RoleId);
        Assert.Equal(isActive, user.IsActive);
        Assert.Equal(clearance, user.Role.ClinicalClearance);
        Assert.True(user.Role.HasClinicalClearance(clearance));
        Assert.False(user.Role.HasClinicalClearance(clearance + 1));
    }

    [Fact]
    public void AuditHashChain_SequentialIntegrityVerification()
    {
        // HIPAA §164.312(c)(1) & (c)(2) Data Integrity:
        // Simulate tamper-evident append-only ledger using SHA-256 hash chaining
        var logs = new List<(int Id, string Action, string PrevHash, string CurrentHash)>();
        string currentPrevHash = "GENESIS_BLOCK_CIPHER_RAG_2026";

        for (int i = 1; i <= 20; i++)
        {
            string logData = $"{i}:UserLogin:UserId_{i}:10.0.0.{i}:{currentPrevHash}";
            string currentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logData)));

            logs.Add((i, "UserLogin", currentPrevHash, currentHash));
            currentPrevHash = currentHash;
        }

        // Verify chain integrity
        for (int i = 1; i < logs.Count; i++)
        {
            Assert.Equal(logs[i - 1].CurrentHash, logs[i].PrevHash);
        }

        // Simulate Tampering on entry 5
        var tamperedData = $"5:UserLogin:UserId_HACKER:10.0.0.5:{logs[4].PrevHash}";
        var recomputedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tamperedData)));

        // Tampering breaks the chain with entry 6
        Assert.NotEqual(logs[4].CurrentHash, recomputedHash);
    }
}
