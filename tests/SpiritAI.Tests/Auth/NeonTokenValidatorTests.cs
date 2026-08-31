using System.Security.Claims;

using Xunit;

namespace SpiritAI.Tests.Auth;

/// <summary>
/// The check that stands between a stranger and the conversation endpoint.
/// </summary>
/// <remarks>
/// Every case here is a way in if it is wrong. The signature ones matter most: .NET cannot verify
/// Ed25519 on its own, so this is hand-written code doing crypto, and hand-written crypto that is
/// never tested against a bad signature is decoration.
/// </remarks>
public sealed class NeonTokenValidatorTests
{
    [Fact]
    public async Task Accepts_ATokenNeonSigned()
    {
        var kit = new NeonAuthTestKit();

        var result = await kit.Validator().ValidateAsync(kit.Token(), TestContext.Current.CancellationToken);

        Assert.Null(result.Failure);
        Assert.NotNull(result.Principal);
        Assert.Equal("user_123", result.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("someone@example.com", result.Principal.FindFirstValue(ClaimTypes.Email));
    }

    [Fact]
    public async Task Rejects_ATamperedSignature()
    {
        var kit = new NeonAuthTestKit();

        var result = await kit.Validator().ValidateAsync(kit.Token(corruptSignature: true), TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
        Assert.Equal("the signature does not verify.", result.Failure);
    }

    [Fact]
    public async Task Rejects_ClaimsEditedAfterSigning()
    {
        var kit = new NeonAuthTestKit();
        var token = kit.Token();

        // Swap one character of the payload. The signature covers it, so this must not get through
        // however plausible the new payload looks.
        var parts = token.Split('.');
        parts[1] = parts[1][..^1] + (parts[1][^1] == 'A' ? 'B' : 'A');

        var result = await kit.Validator().ValidateAsync(string.Join('.', parts), TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("HS256")]
    [InlineData("RS256")]
    public async Task Rejects_AnyAlgorithmButEdDsa(string algorithm)
    {
        var kit = new NeonAuthTestKit();

        var result = await kit.Validator().ValidateAsync(
            kit.Token(algorithm: algorithm),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
        Assert.Equal("the token is not signed with EdDSA.", result.Failure);
    }

    [Fact]
    public async Task Rejects_AnExpiredToken()
    {
        var kit = new NeonAuthTestKit();
        var token = kit.Token();

        // Neon's tokens live 15 minutes; the default skew is a minute.
        kit.Clock.Now = kit.Clock.Now.AddMinutes(20);

        var result = await kit.Validator().ValidateAsync(token, TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
        Assert.Equal("the token has expired.", result.Failure);
    }

    [Fact]
    public async Task Accepts_AJustExpiredTokenWithinTheSkew()
    {
        var kit = new NeonAuthTestKit();
        var token = kit.Token(expires: kit.Clock.GetUtcNow().AddSeconds(-30));

        var result = await kit.Validator().ValidateAsync(token, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Principal);
    }

    [Fact]
    public async Task Rejects_ATokenFromAnotherIssuer()
    {
        var kit = new NeonAuthTestKit();

        var result = await kit.Validator().ValidateAsync(
            kit.Token(issuer: "https://someone-elses.neon.tech"),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
        Assert.Equal("the token was issued by someone else.", result.Failure);
    }

    [Fact]
    public async Task Rejects_ATokenForAnotherAudience()
    {
        var kit = new NeonAuthTestKit();

        var result = await kit.Validator().ValidateAsync(
            kit.Token(audience: "https://some-other-service"),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
        Assert.Equal("the token was issued for a different audience.", result.Failure);
    }

    [Fact]
    public async Task Rejects_ATokenNamingAKeyNeonDoesNotPublish()
    {
        var kit = new NeonAuthTestKit();

        var result = await kit.Validator().ValidateAsync(
            kit.Token(keyId: "some-other-key"),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
        Assert.Equal("no published key is named some-other-key.", result.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("only.two")]
    [InlineData("three.parts.")]
    [InlineData("!!!.!!!.!!!")]
    public async Task Rejects_Rubbish(string token)
    {
        var kit = new NeonAuthTestKit();

        var result = await kit.Validator().ValidateAsync(token, TestContext.Current.CancellationToken);

        Assert.Null(result.Principal);
    }
}
