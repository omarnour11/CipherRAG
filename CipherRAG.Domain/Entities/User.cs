namespace CipherRAG.Domain.Entities;

public class User
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<Document> Documents { get; set; } = [];
    public Role? Role { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<RagRequest> RagRequests { get; set; } = [];

    /// <summary>
    /// Returns true if this user has a populated password hash.
    /// Actual BCrypt verification is performed by IPasswordHasher in the Application layer
    /// (Domain must remain framework-agnostic — HIPAA §164.312(d)).
    /// </summary>
    public bool HasPasswordHash() => !string.IsNullOrWhiteSpace(PasswordHash);
}