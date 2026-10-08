namespace CipherRAG.Infrastructure.Services;

/// <summary>
/// BCrypt-based password hasher implementation.
/// HIPAA §164.312(d) — Unique user identification and authentication.
/// Uses BCrypt work factor 12 (OWASP recommended minimum for HIPAA systems).
/// </summary>
public class BcryptPasswordHasher : CipherRAG.Application.Interfaces.IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plainTextPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextPassword);
        return BCrypt.Net.BCrypt.HashPassword(plainTextPassword, WorkFactor);
    }

    public bool Verify(string plainTextPassword, string hash)
    {
        if (string.IsNullOrWhiteSpace(plainTextPassword) || string.IsNullOrWhiteSpace(hash))
            return false;
        return BCrypt.Net.BCrypt.Verify(plainTextPassword, hash);
    }
}