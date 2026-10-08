using System.Security.Cryptography;
using System.Text;

namespace CipherRAG.Domain.Entities;

public class RefreshToken
{
    public Guid TokenId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the raw refresh token for secure storage.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public string? DevicePlatform { get; set; }
    public string? DeviceId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Null when token is active; populated on revocation (HIPAA audit trail).</summary>
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsRevoked { get; set; }

    // Computed Properties
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation Property
    public User? User { get; private set; }

    // Parameterless constructor required by EF Core and seeding
    public RefreshToken() { }

    private RefreshToken(Guid userId, string rawToken, DateTimeOffset expiresAt, string? devicePlatform = null, string? deviceId = null)
    {
        TokenId = Guid.NewGuid();
        UserId = userId;
        TokenHash = HashToken(rawToken);
        DevicePlatform = devicePlatform;
        DeviceId = deviceId;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Factory method to create a RefreshToken with validation checks.
    /// Stores only a hash of the raw token in the database.
    /// </summary>
    public static RefreshToken Create(Guid userId, string rawToken, DateTimeOffset expiresAt, string? devicePlatform = null, string? deviceId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentNullException(nameof(rawToken), "Token value cannot be empty.");
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Expiration time must be in the future.", nameof(expiresAt));
        }

        return new RefreshToken(
            userId,
            rawToken,
            expiresAt,
            devicePlatform,
            deviceId
        );
    }

    public bool Matches(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        return string.Equals(TokenHash, HashToken(rawToken), StringComparison.Ordinal);
    }

    public static string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentNullException(nameof(rawToken), "Token value cannot be empty.");
        }

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// HIPAA §164.312(a)(2)(iii) — Automatic session termination.
    /// Records exact revocation timestamp for tamper-evident audit trail.
    /// </summary>
    public void Revoke()
    {
        if (IsRevoked) return;

        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}