using System.Net;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;

using Xunit;

using ZiggyCreatures.Caching.Fusion;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// How many staff the bot and the widget are told are online, read from Chatwoot's agent list.
/// The saved reply is a live Chatwoot's, with the administrator's dashboard open and the service
/// user's not.
/// </summary>
public sealed class ChatwootStaffPresenceTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly ChatwootOptions Configured = new()
    {
        BaseUrl = "http://chatwoot.test/",
        AccountId = 2,
        ServiceToken = "service-token",
    };

    [Fact]
    public async Task OnlyAgentsChatwootShowsOnlineAreCounted()
    {
        var presence = Presence(new ReplayingHandler("agents"), Configured);

        Assert.Equal(1, await presence.CountOnlineAsync(Cancel));
    }

    [Fact]
    public async Task ASecondReadSoonAfterDoesNotCallChatwootAgain()
    {
        var wire = new ReplayingHandler("agents");
        var presence = Presence(wire, Configured);

        await presence.CountOnlineAsync(Cancel);
        await presence.CountOnlineAsync(Cancel);

        Assert.Single(wire.Requests);
    }

    [Fact]
    public async Task WhenChatwootFailsNobodyIsOnline()
    {
        var presence = Presence(new ReplayingHandler(payload: null, HttpStatusCode.InternalServerError), Configured);

        Assert.Equal(0, await presence.CountOnlineAsync(Cancel));
    }

    [Fact]
    public async Task WithNoServiceTokenNobodyIsOnlineAndChatwootIsNotCalled()
    {
        var wire = new ReplayingHandler("agents");
        var presence = Presence(wire, new ChatwootOptions { BaseUrl = "http://chatwoot.test/", AccountId = 2 });

        Assert.Equal(0, await presence.CountOnlineAsync(Cancel));
        Assert.Empty(wire.Requests);
    }

    private static ChatwootStaffPresence Presence(ReplayingHandler wire, ChatwootOptions settings)
    {
        var options = Options.Create(settings);
        var cache = new ServiceCollection()
            .AddFusionCache()
            .AsHybridCache()
            .Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();

        return new ChatwootStaffPresence(
            new ChatwootClient(new HttpClient(wire), options),
            cache,
            options,
            NullLogger<ChatwootStaffPresence>.Instance);
    }
}
