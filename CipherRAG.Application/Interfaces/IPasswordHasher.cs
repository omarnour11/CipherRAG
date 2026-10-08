namespace CipherRAG.Application.Interfaces;

/// <summary>
/// HIPAA §164.312(d) — Authentication: provides cryptographic password hashing and verification.
/// Implemented in Infrastructure using BCrypt to keep Domain free of framework dependencies.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainTextPassword);
    bool Verify(string plainTextPassword, string hash);
}