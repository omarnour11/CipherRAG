using CipherRAG.Application.DTOs;

namespace CipherRAG.Application.Interfaces;

public interface IRagService
{
    Task<RagAskResponse> AskAsync(RagAskRequest request, CancellationToken cancellationToken = default);
}
