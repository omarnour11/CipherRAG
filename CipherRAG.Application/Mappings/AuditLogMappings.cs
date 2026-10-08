using CipherRAG.Application.DTOs;
using CipherRAG.Domain.Entities;

namespace CipherRAG.Application.Mappings;

/// <summary>
/// Extension methods for mapping Domain entities to Application DTOs.
/// HIPAA Â§164.312(b) â€” Audit controls: RawPrompt is never stored in DB;
/// only MaskedPrompt is persisted. This mapping exposes only what is needed.
/// </summary>
public static class AuditLogMappings
{
    public static AuditLogDto ToDto(this AuditLogEntry entry) =>
        new AuditLogDto
        {
            LogId             = entry.LogId,
            TsUtc             = entry.TsUtc,
            RawPrompt         = entry.RawPrompt,        // â†  correct field (raw before masking)
            MaskedPrompt      = entry.MaskedPrompt,     // â†  masked version stored in DB
            FinalAnswer       = !string.IsNullOrEmpty(entry.MaskedFinalAnswer) ? entry.MaskedFinalAnswer : entry.FinalAnswer,
            MaskedFinalAnswer = entry.MaskedFinalAnswer,
            ActionStatus      = entry.ActionStatus.ToString(),
            InjectionDetected = entry.InjectionDetected,
            PoisonDetected    = entry.PoisonDetected,
            GroundingScore    = entry.GroundingScore,
            UserId            = entry.UserId,
            RowHash           = entry.RowHash
        };
}