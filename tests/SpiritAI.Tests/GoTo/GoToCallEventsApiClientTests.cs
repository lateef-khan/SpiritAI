using System.Net;

using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// The subscription calls, sent through the host's own GoTo registration. The replies are the ones
/// GoTo sent on 2026-09-24, kept in <c>Payloads</c> with the account key replaced.
/// </summary>
public sealed class GoToCallEventsApiClientTests
{
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
    public async Task TheSubscribedAccountsAreReadForOneChannel()
    {
        var wire = new ReplayingHandler("subscriptions_read") { Folder = "GoTo" };

        var accounts = await Client(wire).ReadSubscribedAccountKeysAsync("Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f", Cancel);

        Assert.Equal(["1234567890123456789"], accounts);
        Assert.Equal(
            "https://api.goto.com/call-events/v1/subscriptions?channelId=Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f",
            Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task AChannelWithNoSubscriptionReadsAsNoAccounts()
    {
        var wire = new ReplayingHandler("subscriptions_none") { Folder = "GoTo" };

        var accounts = await Client(wire).ReadSubscribedAccountKeysAsync("Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f", Cancel);

        Assert.Empty(accounts);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static IGoToCallEventsApiClient Client(ReplayingHandler wire)
        => GoToTestServices.Build(wire).GetRequiredService<IGoToCallEventsApiClient>();
}
