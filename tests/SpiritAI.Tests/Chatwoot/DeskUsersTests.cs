using System.Net;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// Desk users through Chatwoot's Platform API. The replies are the ones a live Chatwoot sent,
/// kept in <c>Payloads</c>.
/// </summary>
public sealed class DeskUsersTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(4, false)]
    public async Task AUserOnTheAgentList_IsInTheAccount_WhateverItsRole(int userId, bool expected)
    {
        var wire = new ReplayingHandler("agents") { Folder = "Hub" };

        Assert.Equal(expected, await Users(wire).IsInAccountAsync(userId, Cancel));
        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/agents", request.Url);
        Assert.Equal("admin-token", request.Token);
    }

    [Fact]
    public async Task ANewUser_IsMadeWithThePlatformToken_AndNoEmailIsAsked()
    {
        var wire = new ReplayingHandler("platform_user_created") { Folder = "Hub" };

        var id = await Users(wire).CreateUserAsync("Lateef Khan", "lateef.khan@spiritfitness.test", Cancel);

        Assert.Equal(4, id);
        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/platform/api/v1/users", request.Url);
        Assert.Equal("platform-token", request.Token);
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal("Lateef Khan", (string?)body["name"]);
        Assert.Equal("lateef.khan@spiritfitness.test", (string?)body["email"]);
        Assert.True(((string?)body["password"])!.Length >= 32);
    }

    [Fact]
    public async Task LeavingTheAccount_DeletesTheAccountUser_WithThePlatformToken()
    {
        var wire = new ReplayingHandler(payload: null) { Folder = "Hub" };

        await Users(wire).LeaveAccountAsync(7, Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("DELETE", request.Method);
        Assert.Equal("http://chatwoot.test/platform/api/v1/accounts/2/account_users", request.Url);
        Assert.Equal("""{"user_id":7}""", request.Body);
        Assert.Equal("platform-token", request.Token);
    }

    [Fact]
    public async Task JoiningTheAccount_AsksForTheAgentRole()
    {
        var wire = new ReplayingHandler("account_user_created") { Folder = "Hub" };

        await Users(wire).JoinAccountAsync(7, Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/platform/api/v1/accounts/2/account_users", request.Url);
        Assert.Equal("""{"user_id":7,"role":"agent"}""", request.Body);
    }

    [Fact]
    public async Task JoiningTheAccountTwice_Answers200_AndIsNotAnError()
    {
        var wire = new ReplayingHandler("account_user_again", HttpStatusCode.OK) { Folder = "Hub" };

        await Users(wire).JoinAccountAsync(7, Cancel);
    }

    [Fact]
    public async Task JoiningTheInbox_AddsOneMember_WithTheAdminToken()
    {
        var wire = new ReplayingHandler("inbox_members") { Folder = "Hub" };

        await Users(wire).JoinInboxAsync(7, Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/inbox_members", request.Url);
        Assert.Equal("""{"inbox_id":1,"user_ids":[7]}""", request.Body);
        Assert.Equal("admin-token", request.Token);
    }

    [Fact]
    public async Task TheSignInLink_IsTheUrlChatwootGives()
    {
        var wire = new ReplayingHandler("sso_link") { Folder = "Hub" };

        var link = await Users(wire).SignInLinkAsync(7, Cancel);

        Assert.Equal(
            "http://desk.spirit.localhost:53000/app/login?email=lateef.khan%40spiritfitness.test&sso_auth_token=REDACTED",
            link);
        Assert.Equal("http://chatwoot.test/platform/api/v1/users/7/login", Assert.Single(wire.Requests).Url);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static DeskUsers Users(ReplayingHandler wire) => new(
        new HttpClient(wire),
        Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test", AccountId = 2, InboxId = 1,
            PlatformToken = "platform-token", AdminToken = "admin-token",
        }));
}
