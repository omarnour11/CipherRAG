using CipherRAG.Domain.Entities;

namespace CipherRAG.Application.Interfaces;

public interface IGuardrailService
{
    Task<RiskAssessment> EvaluateRiskAsync(string rawPrompt, string maskedPrompt, CancellationToken ct = default);
}
