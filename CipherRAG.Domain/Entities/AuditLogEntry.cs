namespace CipherRAG.Domain.Entities;

using CipherRAG.Domain.Enums;

public class AuditLogEntry
{
    public long LogId { get; set; }
    public DateTimeOffset TsUtc { get; set; } = DateTimeOffset.UtcNow;
    public string MaskedPrompt { get; set; } = string.Empty;
    public string MaskedFinalAnswer { get; set; } = string.Empty;
    /// <summary>Original prompt as submitted by the user — before any PII masking.</summary>
    public string RawPrompt { get; set; } = string.Empty;
    /// <summary>Final answer returned to the user — with real values unmasked.</summary>
    public string FinalAnswer { get; set; } = string.Empty;
    public ActionStatus ActionStatus { get; set; }

    public List<Guid> RetrievedChunkIds { get; set; } = [];
    public bool InjectionDetected { get; set; }
    public bool PoisonDetected { get; set; }
    public decimal GroundingScore { get; set; }
    public string RowHash { get; set; } = string.Empty;
    public string PrevRowHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? RoleId { get; set; }
    public Role? Role { get; set; }

    public Guid? RiskAssessmentId { get; set; }
    public Guid? SessionId { get; set; }

    public Guid? RequestId { get; set; }
    public RagRequest? RagRequest { get; set; }

    public MaskingSession? MaskingSession { get; set; }

    public bool VerifyChain(AuditLogEntry? prevEntry)
    {
        if (prevEntry is null)
            return string.IsNullOrEmpty(PrevRowHash);

        return PrevRowHash == prevEntry.RowHash;
    }
}