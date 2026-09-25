using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// The channel and subscription calls, sent through the host's own GoTo registration. The replies
/// are the ones GoTo sent on 2026-09-24, kept in <c>Payloads</c>.
/// </summary>
public sealed class GoToCallEventsApiClientTests
{
    [Fact]
    public async Task AWebhookChannelIsMadeWithTheBodyGoToDocuments()
    {
        var wire = new ReplayingHandler("channel_created", HttpStatusCode.Created) { Folder = "GoTo" };

        var channel = await Client(wire).CreateWebhookChannelAsync(
            "spiritprobe", new Uri("https://spirit.example.test/goto/webhook/probe"), Cancel);

        Assert.Equal(new GoToChannel("Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f", "spiritprobe", 2147483647), channel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("https://api.goto.com/notification-channel/v1/channels/spiritprobe", request.Url);
        Assert.Equal(
            """{"channelType":"Webhook","webhookChannelData":{"webhook":{"url":"https://spirit.example.test/goto/webhook/probe"}}}""",
            request.Body);
        Assert.Equal("Bearer fake-goto-access-token", request.Authorization);
    }

    [Fact]
    public async Task AMultiStatusSubscriptionIsASuccess()
    {
        var wire = new ReplayingHandler("subscription_created", HttpStatusCode.MultiStatus) { Folder = "GoTo" };

        await Client(wire).SubscribeToCallEventsAsync("Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f", Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.goto.com/call-events/v1/subscriptions", request.Url);
        Assert.Equal(
            """{"channelId":"Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f","accountKeys":[{"id":"1234567890123456789","events":["STARTING","ENDING"]}]}""",
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

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>The client as the host builds it, retry included, with GoTo swapped for <paramref name="wire"/>.</summary>
    private static IGoToCallEventsApiClient Client(ReplayingHandler wire)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Goto:AccountKey"] = "1234567890123456789" })
            .Build();

        var services = new ServiceCollection().AddGoToHttpClients(configuration);

        services.AddHttpClient(nameof(IGoToCallEventsApiClient)).ConfigurePrimaryHttpMessageHandler(() => wire);
        services.AddSingleton<IGoToAuthTokenProvider>(new FixedToken());

        return services.BuildServiceProvider().GetRequiredService<IGoToCallEventsApiClient>();
    }

    private sealed class FixedToken : IGoToAuthTokenProvider
    {
        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("fake-goto-access-token");

        public Task InvalidateBearerTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
