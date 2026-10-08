using CipherRAG.Infrastructure.Services;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void HashAndVerify_RoundTripsPassword()
    {
        var hasher = new BcryptPasswordHasher();
        const string password = "Correct Horse Battery Staple!42";

        var hash = hasher.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(hasher.Verify(password, hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }
}
