using CipherRAG.Domain.ValueObjects;

namespace CipherRAG.Domain.Entities;

public class MaskingSession
{
    public Guid SessionId { get; set; }
    public Guid RequestId { get; set; }
    public RagRequest? RagRequest { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    
    public List<MaskedToken> MaskedTokens { get; set; } = [];

    // 💡 Business Methods
    public bool IsExpired()
    {
        return DateTimeOffset.UtcNow >= ExpiresAt;
    }
}