using SpiritAI.Auth;

using Xunit;

namespace SpiritAI.Tests.Auth;

/// <summary>The two values derived from the one configured. Both are easy to get subtly wrong and
/// neither fails loudly when it is.</summary>
public sealed class NeonAuthOptionsTests
{
    [Theory]
    [InlineData("https://ep-x.neon.tech/neondb/auth")]
    // A trailing slash must not swallow the "auth" segment when the relative path is resolved.
    [InlineData("https://ep-x.neon.tech/neondb/auth/")]
    public void JwksUri_SitsBesideTheBaseUrl(string baseUrl)
    {
        var options = new NeonAuthOptions { BaseUrl = baseUrl };

        Assert.Equal(
            new Uri("https://ep-x.neon.tech/neondb/auth/.well-known/jwks.json"),
            options.JwksUri);
    }

    [Fact]
    public void Issuer_IsTheOrigin_NotTheWholeUrl()
    {
        var options = new NeonAuthOptions { BaseUrl = "https://ep-x.neon.tech/neondb/auth" };

        Assert.Equal("https://ep-x.neon.tech", options.Issuer);
    }
}
