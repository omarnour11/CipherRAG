using CipherRAG.Application.DTOs;
using CipherRAG.Application.Responses;

namespace CipherRAG.Application.Interfaces;



public interface ITokenProvider
{
    Task<TokenResponse> GenerateJwtTokenAsync (UserDto user ,CancellationToken ct = default);
    Task RevokeRefreshTokensAsync(Guid userId, CancellationToken ct = default);
}