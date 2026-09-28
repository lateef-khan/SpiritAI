using System.Net;

using Microsoft.Extensions.Options;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// The PAT swap at the wire. The replies are the ones GoTo's token host sent on 2026-09-24, kept
/// in <c>Payloads</c> with the token replaced.
/// </summary>
public sealed class GoToAuthTokenProviderTests
{
    [Fact]
    public async Task ThePatIsSwappedOnceAndTheTokenIsKept()
    {
        var wire = new ReplayingHandler("token_swapped") { Folder = "GoTo" };
        var provider = Provider(wire, Configured);

        var first = await provider.GetBearerTokenAsync(Cancel);
        var second = await provider.GetBearerTokenAsync(Cancel);

        Assert.Equal("fake-goto-access-token", first);
        Assert.Equal(first, second);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://authentication.logmeininc.com/oauth/token", request.Url);
        Assert.Equal("grant_type=personal_access_token&pat=probe-pat", request.Body);
        Assert.Equal("Basic Y2xpZW50LWlkOmNsaWVudC1zZWNyZXQ=", request.Authorization);
    }

    [Fact]
    public async Task AnInvalidatedTokenIsSwappedAgain()
    {
        var wire = new ReplayingHandler("token_swapped") { Folder = "GoTo" };
        var provider = Provider(wire, Configured);

        await provider.GetBearerTokenAsync(Cancel);
        await provider.InvalidateBearerTokenAsync(Cancel);
        await provider.GetBearerTokenAsync(Cancel);

        Assert.Equal(2, wire.Requests.Count);
    }

    [Fact]
    public async Task AMissingPatIsNamedAndNothingIsSent()
    {
        var wire = new ReplayingHandler("token_swapped") { Folder = "GoTo" };
        var provider = Provider(wire, new GoToOptions { ClientId = "client-id", ClientSecret = "client-secret" });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetBearerTokenAsync(Cancel));

        Assert.Equal("Goto:PersonalAccessToken is missing.", error.Message);
        Assert.Empty(wire.Requests);
    }

    [Fact]
    public async Task ARefusedSwapThrowsWithTheStatusAndGoTosReason()
    {
        var wire = new ReplayingHandler("token_invalid_client", HttpStatusCode.Unauthorized) { Folder = "GoTo" };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Provider(wire, Configured).GetBearerTokenAsync(Cancel));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Contains("401", error.Message, StringComparison.Ordinal);
        Assert.Contains("invalid_client", error.Message, StringComparison.Ordinal);
    }

    private static GoToOptions Configured => new()
    {
        ClientId = "client-id",
        ClientSecret = "client-secret",
        PersonalAccessToken = "probe-pat",
    };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static GoToAuthTokenProvider Provider(ReplayingHandler wire, GoToOptions options)
        => new(
            new HttpClient(wire) { BaseAddress = GoToAuthTokenProvider.TokenHost },
            TestHybridCache.Create(),
            TimeProvider.System,
            Options.Create(options));
}
