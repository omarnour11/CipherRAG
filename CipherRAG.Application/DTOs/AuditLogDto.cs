namespace CipherRAG.Application.DTOs;
public class AuditLogDto
{
    public long LogId { get; set; }
    public DateTimeOffset TsUtc { get; set; }
    /// <summary>The original prompt as submitted by the user (before masking).</summary>
    public string RawPrompt { get; set; } = string.Empty;
    /// <summary>The prompt after PII/PCI masking has been applied (tokens replaced with [MASKED_*] placeholders).</summary>
    public string MaskedPrompt { get; set; } = string.Empty;
    public string FinalAnswer { get; set; } = string.Empty;
    /// <summary>The final answer after PII/PHI de-identification per HIPAA §164.514.</summary>
    public string MaskedFinalAnswer { get; set; } = string.Empty;
    public string ActionStatus { get; set; } = string.Empty;
    public bool InjectionDetected { get; set; }
    public bool PoisonDetected { get; set; }
    public decimal GroundingScore { get; set; }
    public Guid UserId { get; set; }
    public string RowHash { get; set; } = string.Empty;
}