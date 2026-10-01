using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
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

    private const string Channel = "Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f";

    [Fact]
    public async Task AStaleChannelIsDeletedAndANewOneMadeAndSubscribedToCallsAndReports()
    {
        var wire = new ReplayingHandler(
        [
            ("channels_stale", HttpStatusCode.OK),
            (null, HttpStatusCode.NoContent),
            ("channel_created", HttpStatusCode.Created),
            ("subscriptions_none", HttpStatusCode.OK),
            ("subscription_created", HttpStatusCode.MultiStatus),
            ("report_subscriptions_none", HttpStatusCode.OK),
            ("report_subscription_created", HttpStatusCode.OK),
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
                $"GET {Api}call-events-report/v1/subscriptions?channelId=Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f",
                $"POST {Api}call-events-report/v1/subscriptions",
            ],
            wire.Requests.Select(r => $"{r.Method} {r.Url}"));
    }

    [Fact]
    public async Task AMatchingChannelSubscribedToBothIsKeptAsItIs()
    {
        var wire = new ReplayingHandler(["channels_matching", "subscriptions_read", "report_subscriptions_read"]) { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        Assert.Equal(
            [
                $"GET {Api}notification-channel/v1/channels?pageSize=100",
                $"GET {Api}call-events/v1/subscriptions?channelId=Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f",
                $"GET {Api}call-events-report/v1/subscriptions?channelId=Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f",
            ],
            wire.Requests.Select(r => $"{r.Method} {r.Url}"));
    }

    [Fact]
    public async Task AChannelWithCallEventsButNoReportsIsSubscribedToReports()
    {
        var wire = new ReplayingHandler(
        [
            ("channels_matching", HttpStatusCode.OK),
            ("subscriptions_read", HttpStatusCode.OK),
            ("report_subscriptions_none", HttpStatusCode.OK),
            ("report_subscription_created", HttpStatusCode.OK),
        ])
        { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        var subscribe = wire.Requests[^1];
        Assert.Equal($"POST {Api}call-events-report/v1/subscriptions", $"{subscribe.Method} {subscribe.Url}");
        Assert.Equal(
            """{"channelId":"Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f","eventTypes":["REPORT_SUMMARY"],"accountKeys":["1234567890123456789"]}""",
            subscribe.Body);
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
            ("report_subscriptions_read", HttpStatusCode.OK),
        ])
        { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        Assert.Equal(["GET", "DELETE", "POST", "GET", "GET"], wire.Requests.Select(r => r.Method));
    }

    [Fact]
    public async Task TheMatchingChannelOnALaterPageIsFound()
    {
        var wire = new ReplayingHandler(["channels_page_1", "channels_matching", "subscriptions_read", "report_subscriptions_read"]) { Folder = "GoTo" };

        await KeepOnceAsync(wire);

        Assert.DoesNotContain(wire.Requests, r => r.Method is "POST" or "DELETE");
    }

    [Fact]
    public async Task AFailedCallEventsCheckStillLeavesTheReportSubscriptionKept()
    {
        var logger = new CapturingLogger<GoToChannelKeeper>();
        var wire = new RoutingHandler(request => request switch
        {
            { Method.Method: "GET" } when request.RequestUri!.AbsolutePath == "/notification-channel/v1/channels" => ("channels_matching", HttpStatusCode.OK),
            { Method.Method: "GET" } when request.RequestUri!.AbsolutePath == "/call-events/v1/subscriptions" => (null, HttpStatusCode.InternalServerError),
            { Method.Method: "GET" } => ("report_subscriptions_none", HttpStatusCode.OK),
            _ => ("report_subscription_created", HttpStatusCode.OK),
        });

        await KeepOnceAsync(wire, logger);

        var requests = wire.Requests.Select(r => $"{r.Method} {r.Url}").ToList();
        var reportRead = requests.IndexOf($"GET {Api}call-events-report/v1/subscriptions?channelId={Channel}");

        Assert.Equal($"GET {Api}notification-channel/v1/channels?pageSize=100", requests[0]);
        Assert.NotEmpty(requests[1..reportRead]);
        Assert.All(requests[1..reportRead], r => Assert.Equal($"GET {Api}call-events/v1/subscriptions?channelId={Channel}", r));
        Assert.Equal([$"POST {Api}call-events-report/v1/subscriptions"], requests[(reportRead + 1)..]);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("Could not keep", StringComparison.Ordinal));
        Assert.Contains("call-events", warning.Message, StringComparison.Ordinal);
        Assert.Contains(Channel, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedReportCheckStillLeavesTheCallEventsSubscriptionKeptAndTheRunDoesNotFail()
    {
        var logger = new CapturingLogger<GoToChannelKeeper>();
        var wire = new ReplayingHandler(
        [
            ("channels_matching", HttpStatusCode.OK),
            ("subscriptions_none", HttpStatusCode.OK),
            ("subscription_created", HttpStatusCode.MultiStatus),
            (null, HttpStatusCode.InternalServerError),
        ])
        { Folder = "GoTo" };

        await KeepOnceAsync(wire, logger);

        Assert.Contains(wire.Requests, r => r is { Method: "POST" } && r.Url == $"{Api}call-events/v1/subscriptions");

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("Could not keep", StringComparison.Ordinal));
        Assert.Contains("call-report", warning.Message, StringComparison.Ordinal);
        Assert.Contains(Channel, warning.Message, StringComparison.Ordinal);
    }

    private static async Task KeepOnceAsync(HttpMessageHandler wire, ILogger<GoToChannelKeeper>? logger = null)
    {
        await using var services = GoToTestServices.Build(wire, new NoWaitClock());

        var keeper = new GoToChannelKeeper(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IOptions<GoToOptions>>(),
            new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance),
            TimeProvider.System,
            logger ?? NullLogger<GoToChannelKeeper>.Instance);

        await keeper.KeepOnceAsync(TestContext.Current.CancellationToken);
    }
}
