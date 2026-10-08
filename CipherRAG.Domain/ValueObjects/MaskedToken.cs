using CipherRAG.Domain.Entities;

using CipherRAG.Domain.Enums;
namespace CipherRAG.Domain.ValueObjects;
public class MaskedToken
{
    public Guid TokenId { get; private set; }
    public string PlaceHolder { get; private set; } = string.Empty;
    public string EncryptedRealValue { get; private set; } = string.Empty;
    public EntityType EntityType { get; private set; }

    public Guid SessionId { get; private set; }
    public MaskingSession? MaskingSession { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    private MaskedToken() { }

    public MaskedToken(string placeholder, string encryptedRealValue, EntityType entityType, Guid sessionId, DateTimeOffset expiresAt)
    {
        TokenId = Guid.NewGuid();
        PlaceHolder = placeholder;
        EncryptedRealValue = encryptedRealValue;
        EntityType = entityType;
        SessionId = sessionId;
        ExpiresAt = expiresAt;
    }

    // ?? Business Methods
    public bool IsExpired() => DateTimeOffset.UtcNow >= ExpiresAt;
}