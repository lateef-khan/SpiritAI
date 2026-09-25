using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// One run of the channel job against GoTo's recorded answers. The webhook URL is
/// <c>https://spirit.example.test/goto/webhook/probe</c> and the nickname is <c>spiritprobe</c>.
/// </summary>
public sealed class GoToChannelKeeperTests
{
    private const string Api = "https://api.goto.com/";

    [Fact]
    public async Task AStaleChannelIsDeletedAndANewOneMadeAndSubscribed()
    {
        var wire = new ReplayingHandler(
        [
            ("channels_stale", HttpStatusCode.OK),
            (null, HttpStatusCode.NoContent),
            ("channel_created", HttpStatusCode.Created),
            ("subscriptions_none", HttpStatusCode.OK),
            ("subscription_created", HttpStatusCode.MultiStatus),
        ])
        { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        // spirit-prod shares the GoTo user and is never touched.
        Assert.Equal(
            [
                $"GET {Api}notification-channel/v1/channels?pageSize=100",
                $"DELETE {Api}notification-channel/v1/channels/spiritprobe/Webhook.9d6e3a10-44f2-4b8e-a1c7-5e2b8f60d913",
                $"POST {Api}notification-channel/v1/channels/spiritprobe",
                $"GET {Api}call-events/v1/subscriptions?channelId=Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f",
                $"POST {Api}call-events/v1/subscriptions",
            ],
            wire.Requests.Select(r => $"{r.Method} {r.Url}"));
    }

    [Fact]
    public async Task AMatchingSubscribedChannelIsKeptAsItIs()
    {
        var wire = new ReplayingHandler(["channels_matching", "subscriptions_read"]) { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        Assert.Equal(
            [
                $"GET {Api}notification-channel/v1/channels?pageSize=100",
                $"GET {Api}call-events/v1/subscriptions?channelId=Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f",
            ],
            wire.Requests.Select(r => $"{r.Method} {r.Url}"));
    }

    [Fact]
    public async Task AStaleChannelThatIsGoneAlreadyStillLeadsToACreate()
    {
        var wire = new ReplayingHandler(
        [
            ("channels_stale", HttpStatusCode.OK),
            ("channel_not_found", HttpStatusCode.NotFound),
            ("channel_created", HttpStatusCode.Created),
            ("subscriptions_read", HttpStatusCode.OK),
        ])
        { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        Assert.Equal(["GET", "DELETE", "POST", "GET"], wire.Requests.Select(r => r.Method));
    }

    [Fact]
    public async Task TheMatchingChannelOnALaterPageIsFound()
    {
        var wire = new ReplayingHandler(["channels_page_1", "channels_matching", "subscriptions_read"]) { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        Assert.DoesNotContain(wire.Requests, r => r.Method is "POST" or "DELETE");
    }

    private static async Task KeepOnceAsync(ReplayingHandler wire)
    {
        await using var services = GoToTestServices.Build(wire);

        var keeper = new GoToChannelKeeper(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IOptions<GoToOptions>>(),
            new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance),
            TimeProvider.System,
            NullLogger<GoToChannelKeeper>.Instance);

        await keeper.KeepOnceAsync(TestContext.Current.CancellationToken);
    }
}
