using CipherRAG.Domain.Entities;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class RefreshTokenTests
{
    [Fact]
    public void Create_StoresOnlyHashAndMatchesRawToken()
    {
        var rawToken = "refresh-token-value";
        var token = RefreshToken.Create(
            Guid.NewGuid(),
            rawToken,
            DateTimeOffset.UtcNow.AddDays(7),
            "Web",
            "test-device");

        Assert.NotEqual(rawToken, token.TokenHash);
        Assert.True(token.Matches(rawToken));
        Assert.False(token.Matches("wrong-token"));
        Assert.True(token.IsActive);
    }

    [Fact]
    public void Revoke_SetsRevocationTimestampAndDisablesToken()
    {
        var token = RefreshToken.Create(
            Guid.NewGuid(),
            "refresh-token-value",
            DateTimeOffset.UtcNow.AddDays(7));

        token.Revoke();

        Assert.True(token.IsRevoked);
        Assert.NotNull(token.RevokedAt);
        Assert.False(token.IsActive);
    }
}
