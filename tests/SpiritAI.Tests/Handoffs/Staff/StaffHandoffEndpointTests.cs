using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Staff;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.Tests.Threads;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Staff;

/// <summary>
/// The inbox over the wire: sections 5 and 9.1 of the handoff spec, with a real token on every request.
/// </summary>
public sealed class StaffHandoffEndpointTests
{
    private const string Handoff = "/v1/handoff";

    [Fact]
    public async Task WithoutATokenTheInboxIsShut()
    {
        await using var world = await StaffHandoffWorld.StartAsync();

        var response = await world.Anonymous.GetAsync(Handoff);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASignedInDealerIsNotStaff()
    {
        await using var world = await StaffHandoffWorld.StartAsync();

        var response = await world.Dealer.GetAsync(Handoff);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheQueueListsWaitingChatsInAskOrder()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var first = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        var second = await world.MakeChatAsync("Console dead", "the console will not turn on");
        await world.AskAsync(first);
        await world.AskAsync(second);

        var page = await world.Staff.ReadAsync<HandoffPage>(Handoff);

        Assert.Equal([first, second], page.Items.Select(item => item.ConversationId));
        Assert.Equal([1, 2], page.Items.Select(item => item.Position));
        Assert.Equal("Belt slips", page.Items[0].Title);
        Assert.Equal("the belt keeps slipping", page.Items[0].FirstLine);
        Assert.All(page.Items, item => Assert.Equal("waiting", item.Status));
    }

    [Fact]
    public async Task AnUnknownStatusIsRefused()
    {
        await using var world = await StaffHandoffWorld.StartAsync();

        var response = await world.Staff.GetAsync($"{Handoff}?status=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AChatThatNeverAskedIsNotFound()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");

        var response = await world.Staff.GetAsync($"{Handoff}/{conversationId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AChatThatAskedAnswersWithItsStatus()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);

        var summary = await world.Staff.ReadAsync<HandoffSummary>($"{Handoff}/{conversationId}");

        Assert.Equal("waiting", summary.Status);
        Assert.Equal(1, summary.Position);
    }

    [Fact]
    public async Task TheWholeChatIsStaffsToReadOnceItAsked()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");

        // A chat that never asked for a person is not staff's to read, however real its id.
        Assert.Equal(HttpStatusCode.NotFound, (await world.Staff.GetAsync($"{Handoff}/{conversationId}/messages")).StatusCode);

        await world.AskAsync(conversationId);
        var history = await world.Staff.ReadAsync<ThreadHistory>($"{Handoff}/{conversationId}/messages");

        Assert.Equal(2, history.Messages.Count);
        Assert.Equal("the belt keeps slipping", Assert.IsType<ThreadTextPart>(history.Messages[0].Message.Content[0]).Text);
    }

    [Fact]
    public async Task ClaimingAWaitingChatTakesItAndSaysSo()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);

        var response = await world.Staff.PostAsync($"{Handoff}/{conversationId}/claim");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.ReadAsync<HandoffSummary>();
        Assert.Equal("human", summary.Status);
        Assert.Equal("Dana R.", summary.Assignee?.Name);
        Assert.Null(summary.Position);

        Assert.Contains("handoff.claimed", world.Notifier.Events);

        var note = await world.LastWordAsync(conversationId);
        Assert.Equal(ChatRole.Assistant, note.Content.Role);
        Assert.Equal("Dana R. joined", note.Content.Text);
        Assert.Equal("system", SpeakerProperty.Read(note.Content)?.GetProperty("kind").GetString());

        var pushed = Assert.Single(world.Notifier.Pushed, push => push.Event == "message.created");
        var line = Assert.IsType<HandoffMessage>(pushed.Payload);
        Assert.Equal("Dana R. joined", line.Text);
        Assert.Equal("system", line.Speaker?.Kind);
        Assert.Equal(note.MessageId, line.MessageId);
    }

    [Fact]
    public async Task ASecondClaimIsRefusedNamingWhoHasIt()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);
        await world.Staff.PostAsync($"{Handoff}/{conversationId}/claim");

        var response = await world.OtherStaff.PostAsync($"{Handoff}/{conversationId}/claim");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Dana R.", await DetailOf(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClaimingAChatNobodyAskedOnIsNotFound()
    {
        await using var world = await StaffHandoffWorld.StartAsync();

        var response = await world.Staff.PostAsync($"{Handoff}/no-such-chat/claim");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AReplyBeforeAClaimIsRefused()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);

        var response = await world.Staff.PostAsync($"{Handoff}/{conversationId}/messages", new { text = "Hello" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheAssigneeMayReply()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);
        await world.Staff.PostAsync($"{Handoff}/{conversationId}/claim");

        var response = await world.OtherStaff.PostAsync($"{Handoff}/{conversationId}/messages", new { text = "Hello" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheAssigneesReplyIsStoredSignedAndPushed()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);
        await world.Staff.PostAsync($"{Handoff}/{conversationId}/claim");

        var response = await world.Staff.PostAsync($"{Handoff}/{conversationId}/messages", new { text = "Try the tension bolt." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<HandoffMessage>();
        Assert.Equal("assistant", created.Role);
        Assert.Equal("Try the tension bolt.", created.Text);
        Assert.Equal("human", created.Speaker?.Kind);
        Assert.Equal("Dana R.", created.Speaker?.Name);

        var stored = await world.LastWordAsync(conversationId);
        Assert.Equal(created.MessageId, stored.MessageId);
        Assert.Equal(ChatRole.Assistant, stored.Content.Role);
        Assert.Equal("Try the tension bolt.", stored.Content.Text);
        Assert.Equal("Dana R.", SpeakerProperty.Read(stored.Content)?.GetProperty("name").GetString());

        Assert.Contains("message.created", world.Notifier.Events);
    }

    [Fact]
    public async Task ABlankReplyIsRefused()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);
        await world.Staff.PostAsync($"{Handoff}/{conversationId}/claim");

        var response = await world.Staff.PostAsync($"{Handoff}/{conversationId}/messages", new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FinishingHandsTheChatBackAndSaysGoodbye()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var conversationId = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        await world.AskAsync(conversationId);
        await world.Staff.PostAsync($"{Handoff}/{conversationId}/claim");

        var response = await world.Staff.PostAsync($"{Handoff}/{conversationId}/done");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HandoffStatus.Done, Assert.Single(world.Store.Rows).Status);
        Assert.Contains("handoff.done", world.Notifier.Events);
        Assert.Equal("Dana R. left", (await world.LastWordAsync(conversationId)).Content.Text);
    }

    [Fact]
    public async Task FinishingAWaitingChatMovesTheOnesBehindItUp()
    {
        await using var world = await StaffHandoffWorld.StartAsync();
        var first = await world.MakeChatAsync("Belt slips", "the belt keeps slipping");
        var second = await world.MakeChatAsync("Console dead", "the console will not turn on");
        await world.AskAsync(first);
        await world.AskAsync(second);

        var response = await world.Staff.PostAsync($"{Handoff}/{first}/done");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(("handoff.queue", (object)(second, 1)), world.Notifier.Pushed);
    }

    [Fact]
    public async Task FinishingAChatNobodyAskedOnIsNotFound()
    {
        await using var world = await StaffHandoffWorld.StartAsync();

        var response = await world.Staff.PostAsync($"{Handoff}/no-such-chat/done");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Reads the <c>detail</c> of a problem response.</summary>
    private static async Task<string> DetailOf(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return problem.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() ?? string.Empty : string.Empty;
    }
}
