using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using SpiritAI.Handoffs.Desk;
using SpiritAI.Tests.Auth;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Desk;

/// <summary>
/// The desk over its fakes: what the routes and the bot's tool share.
/// </summary>
public sealed class HandoffDeskTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeHandoffStore _store;
    private readonly IConversations _conversations;
    private readonly RecordingHandoffNotifier _notifier = new();
    private readonly HandoffDesk _desk;

    public HandoffDeskTests()
    {
        _store = new FakeHandoffStore(_clock);
        _conversations = new Conversations(new InMemoryConversationStore(_clock), blobs: null);
        _desk = new HandoffDesk(_store, _conversations, _notifier, new FakeStaffPresence(), _clock);
    }

    [Fact]
    public async Task TheVisitorCannotSpeakToAChatTheBotHas()
    {
        var conversationId = Guid.NewGuid().ToString("N");
        await _conversations.CreateAsync(conversationId, Cancel);

        var said = await _desk.VisitorSaysAsync(conversationId, "Still there?", Cancel);

        Assert.Null(said);
        Assert.Empty(await _conversations.AllAsync(conversationId, Cancel));
        Assert.Empty(_notifier.Events);
    }
}
