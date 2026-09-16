using System.Net;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.Tests.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Visitors;

/// <summary>
/// The visitor's side over the wire: sections 5 and 9.2 of the handoff spec, with the widget's
/// key on every request.
/// </summary>
public sealed class VisitorHandoffEndpointTests
{
    private const string Handoff = "/v1/public/handoff";

    [Fact]
    public async Task AskingJoinsTheQueueAndTellsStaff()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        await world.StaffOnlineAsync(2);
        var callId = await world.MakeChatAsync(world.Visitor);

        var response = await world.Visitor.PostAsync(Handoff, new { callId, reason = "I want a person" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var state = await response.ReadAsync<HandoffState>();
        Assert.Equal("waiting", state.Status);
        Assert.Equal(1, state.Position);
        Assert.Equal(2, state.StaffOnline);

        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(HandoffAskedBy.Visitor, row.AskedBy);
        Assert.Equal("I want a person", row.Reason);
        Assert.Equal(["handoff.waiting"], world.Notifier.Events);
    }

    [Fact]
    public async Task AskingAgainChangesNothing()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);
        await world.Visitor.PostAsync(Handoff, new { callId });

        var response = await world.Visitor.PostAsync(Handoff, new { callId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("waiting", (await response.ReadAsync<HandoffState>()).Status);
        Assert.Single(world.Store.Rows);
        Assert.Equal(["handoff.waiting"], world.Notifier.Events);
    }

    [Fact]
    public async Task AnotherVisitorCannotAskOnTheChat()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);

        var response = await world.Stranger.PostAsync(Handoff, new { callId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(world.Store.Rows);
    }

    [Fact]
    public async Task WithoutAKeyNothingIsAsked()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);

        var response = await world.Anonymous.PostAsync(Handoff, new { callId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AChatNobodyAskedOnIsWithTheBot()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);

        var state = await world.Visitor.ReadAsync<HandoffState>($"{Handoff}/{callId}");

        Assert.Equal("bot", state.Status);
        Assert.Null(state.Position);
        Assert.Null(state.AssigneeName);
    }

    [Fact]
    public async Task AChatSomebodyTookNamesThem()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);
        await world.Store.AskAsync(callId, HandoffAskedBy.Visitor, null, TestContext.Current.CancellationToken);
        await world.Store.ClaimAsync(callId, "user:dana", "Dana R.", TestContext.Current.CancellationToken);

        var state = await world.Visitor.ReadAsync<HandoffState>($"{Handoff}/{callId}");

        Assert.Equal("human", state.Status);
        Assert.Equal("Dana R.", state.AssigneeName);
        Assert.Null(state.Position);
    }

    [Fact]
    public async Task AnEmailIsKeptOnTheOpenRow()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);
        await world.Visitor.PostAsync(Handoff, new { callId });

        var response = await world.Visitor.PatchAsync($"{Handoff}/{callId}/email", new { email = "pat@example.com" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("pat@example.com", Assert.Single(world.Store.Rows).Email);

        var state = await world.Visitor.ReadAsync<HandoffState>($"{Handoff}/{callId}");
        Assert.Equal("pat@example.com", state.Email);
    }

    [Fact]
    public async Task SomethingThatIsNotAnAddressIsRefused()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);
        await world.Visitor.PostAsync(Handoff, new { callId });

        var response = await world.Visitor.PatchAsync($"{Handoff}/{callId}/email", new { email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(Assert.Single(world.Store.Rows).Email);
    }

    [Fact]
    public async Task AnEmailWithNothingWaitingHasNowhereToGo()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);

        var response = await world.Visitor.PatchAsync($"{Handoff}/{callId}/email", new { email = "pat@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AMessageWhileWaitingIsStoredAndPushed()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);
        await world.Visitor.PostAsync(Handoff, new { callId });

        var response = await world.Visitor.PostAsync($"{Handoff}/{callId}/messages", new { text = "Still there?" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<HandoffMessage>();
        Assert.Equal("user", created.Role);
        Assert.Equal("Still there?", created.Text);
        Assert.Null(created.Speaker);

        var stored = (await world.WordsAsync(callId))[^1];
        Assert.Equal(created.MessageId, stored.MessageId);
        Assert.Equal(ChatRole.User, stored.Content.Role);
        Assert.Equal("Still there?", stored.Content.Text);
        Assert.Null(SpeakerProperty.Read(stored.Content));

        var (name, payload) = Assert.Single(world.Notifier.Pushed, push => push.Event == "message.created");
        Assert.Equal("message.created", name);
        var pushed = Assert.IsType<HandoffMessage>(payload);
        Assert.Equal("user", pushed.Role);
        Assert.Null(pushed.Speaker);
    }

    [Fact]
    public async Task AMessageWhileTheBotHasTheChatBelongsOnTheChatRoute()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);

        var response = await world.Visitor.PostAsync($"{Handoff}/{callId}/messages", new { text = "Still there?" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        // The chat still holds only the turn it was made with.
        Assert.Equal(2, (await world.WordsAsync(callId)).Count);
    }

    [Fact]
    public async Task ABlankMessageIsRefused()
    {
        await using var world = await VisitorHandoffWorld.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor);
        await world.Visitor.PostAsync(Handoff, new { callId });

        var response = await world.Visitor.PostAsync($"{Handoff}/{callId}/messages", new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
