using CipherRAG.Domain.ValueObjects;

namespace CipherRAG.Application.Interfaces;

public interface IPresidioService
{
    Task<(string MaskedPrompt, List<MaskedToken> Tokens)> AnalyzeAndMaskAsync(
        string rawPrompt,
        Guid sessionId,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);
}
