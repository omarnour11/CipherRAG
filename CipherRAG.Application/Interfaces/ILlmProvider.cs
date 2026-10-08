namespace CipherRAG.Application.Interfaces;

public interface ILlmProvider
{
    Task<string> GenerateResponseAsync(string prompt, string context, CancellationToken ct = default);
}
