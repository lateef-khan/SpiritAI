using System.Net;

using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// The channel calls, sent through the host's own GoTo registration. The replies are the ones GoTo
/// sent on 2026-09-24, kept in <c>Payloads</c> with ids, URLs and secrets replaced.
/// </summary>
public sealed class GoToNotificationChannelApiClientTests
{
    [Fact]
    public async Task AWebhookChannelIsMadeWithTheBodyGoToDocuments()
    {
        var wire = new ReplayingHandler("channel_created", HttpStatusCode.Created) { Folder = "GoTo" };

        var channel = await Client(wire).CreateWebhookChannelAsync(
            "spiritprobe", new Uri("https://spirit.example.test/goto/webhook/probe"), Cancel);

        Assert.Equal(
            new GoToChannel(
                "Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f",
                "spiritprobe",
                "https://spirit.example.test/goto/webhook/probe",
                2147483647),
            channel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("https://api.goto.com/notification-channel/v1/channels/spiritprobe", request.Url);
        Assert.Equal(
            """{"channelType":"Webhook","webhookChannelData":{"webhook":{"url":"https://spirit.example.test/goto/webhook/probe"}}}""",
            request.Body);
        Assert.Equal("Bearer fake-goto-access-token", request.Authorization);
    }

    [Fact]
    public async Task APostThatFailsWithAServerErrorIsSentOnce()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.ServiceUnavailable) { Folder = "GoTo" };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Client(wire).CreateWebhookChannelAsync(
            "spiritprobe", new Uri("https://spirit.example.test/goto/webhook/probe"), Cancel));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Single(wire.Requests);
    }

    [Fact]
    public async Task TheListFollowsNextPageMarker()
    {
        var wire = new ReplayingHandler(["channels_page_1", "channels_matching"]) { Folder = "GoTo" };

        var channels = await Client(wire).ListChannelsAsync(Cancel);

        Assert.Equal(["spirit-prod", "spiritprobe"], channels.Select(c => c.Nickname));
        Assert.Equal("https://spirit-prod.example.test/goto/webhook/prod", channels[0].WebhookUrl);
        Assert.Equal(
            [
                "https://api.goto.com/notification-channel/v1/channels?pageSize=100",
                "https://api.goto.com/notification-channel/v1/channels?pageSize=100&pageMarker=ZmFrZS1wYWdlLW1hcmtlcg",
            ],
            wire.Requests.Select(r => r.Url));
    }

    [Fact]
    public async Task ADeleteGoesByNicknameAndId()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.NoContent) { Folder = "GoTo" };

        await Client(wire).DeleteChannelAsync("spiritprobe", "Webhook.9d6e3a10-44f2-4b8e-a1c7-5e2b8f60d913", Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("DELETE", request.Method);
        Assert.Equal(
            "https://api.goto.com/notification-channel/v1/channels/spiritprobe/Webhook.9d6e3a10-44f2-4b8e-a1c7-5e2b8f60d913",
            request.Url);
    }

    [Fact]
    public async Task ADeleteOfAGoneChannelIsASuccess()
    {
        var wire = new ReplayingHandler("channel_not_found", HttpStatusCode.NotFound) { Folder = "GoTo" };

        await Client(wire).DeleteChannelAsync("spiritprobe", "Webhook.9d6e3a10-44f2-4b8e-a1c7-5e2b8f60d913", Cancel);

        Assert.Single(wire.Requests);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static IGoToNotificationChannelApiClient Client(ReplayingHandler wire)
        => GoToTestServices.Build(wire).GetRequiredService<IGoToNotificationChannelApiClient>();
}
