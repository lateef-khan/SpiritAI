using System.Net;

using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// The subscription calls, sent through the host's own GoTo registration. The replies are the ones
/// GoTo sent on 2026-09-24, kept in <c>Payloads</c> with the account key replaced.
/// </summary>
public sealed class GoToClientSubscriptionTests
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
            """{"channelId":"Webhook.1252c4bf-ca42-43c3-8fcb-f3b2c8f0125f","accountKeys":[{"id":"1234567890123456789","events":["STARTING","ACTIVE","ENDING"]}]}""",
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

    [Fact]
    public async Task AReportSubscriptionGoToConfirmsIsASuccess()
    {
        var wire = new ReplayingHandler("report_subscription_created") { Folder = "GoTo" };

        await Client(wire).SubscribeToCallReportsAsync("Webhook.ad561afd-52d4-44af-a905-fd14c637184b", Cancel);

        Assert.Equal("https://api.goto.com/call-events-report/v1/subscriptions", Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task AReportSubscriptionGoToAnswersWithNoItemsIsRefused()
    {
        var wire = new AnsweringHandler("""{"items":[]}""");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(wire).SubscribeToCallReportsAsync("Webhook.ad561afd-52d4-44af-a905-fd14c637184b", Cancel));

        Assert.Contains("""{"items":[]}""", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReportSubscriptionAnsweredWithNoJsonIsRefusedWithGoTosBody()
    {
        var wire = new AnsweringHandler("not json");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(wire).SubscribeToCallReportsAsync("Webhook.ad561afd-52d4-44af-a905-fd14c637184b", Cancel));

        Assert.Contains("not json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReportSubscriptionAnsweredWithAnEmptyBodyIsRefused()
    {
        var wire = new AnsweringHandler(string.Empty);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(wire).SubscribeToCallReportsAsync("Webhook.ad561afd-52d4-44af-a905-fd14c637184b", Cancel));
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static GoToClient Client(HttpMessageHandler wire)
        => GoToTestServices.Build(wire).GetRequiredService<GoToClient>();
}
