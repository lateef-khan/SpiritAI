using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.PublicChat;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Handoffs;
using SpiritAI.Tests.RealTime;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// A staff reply from Chatwoot reaching the visitor: stored in the chat, signed, and pushed. And
/// the name on the visitor's banner following whoever holds the chat.
/// </summary>
public sealed class ChatwootEventHandlerTests
{
    private const string Reply = "Try the tension bolt.";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeHandoffStore _store;
    private readonly IConversations _conversations;
    private readonly RecordingHandoffNotifier _notifier = new();

    public ChatwootEventHandlerTests()
    {
        _store = new FakeHandoffStore(_clock);
        _conversations = new Conversations(new InMemoryConversationStore(_clock), blobs: null);
    }

    [Fact]
    public async Task AStaffReplyIsStoredSignedAndPushed()
    {
        var conversationId = await TakenChatAsync();

        await Handler().HandleAsync(StaffSays(conversationId), Cancel);

        await AssertStoredAndPushedAsync(conversationId);
    }

    [Fact]
    public async Task AChatHandedToAnotherAgentShowsTheirName()
    {
        var conversationId = await TakenChatAsync();

        await Handler().HandleAsync(AssignedTo(conversationId, "Sam K."), Cancel);

        var (name, payload) = Assert.Single(_notifier.Pushed);
        Assert.Equal("handoff.claimed", name);
        Assert.Equal("Sam K.", (((string, HandoffAssignee))payload).Item2.Name);
        Assert.Equal("Sam K.", (await _store.OpenAsync(conversationId, Cancel))?.AssigneeName);
    }

    [Fact]
    public async Task TheSameAgentAgainPushesNothing()
    {
        var conversationId = await TakenChatAsync();

        await Handler().HandleAsync(AssignedTo(conversationId, "Dana R."), Cancel);

        Assert.Empty(_notifier.Pushed);
    }

    /// <summary>The reply is in the chat, signed with the staff name, and was pushed to the visitor.</summary>
    private async Task AssertStoredAndPushedAsync(string conversationId)
    {
        var stored = Assert.Single(await _conversations.AllAsync(conversationId, Cancel));
        Assert.Equal(ChatRole.Assistant, stored.Content.Role);
        Assert.Equal(Reply, stored.Content.Text);
        Assert.Equal("Dana R.", SpeakerProperty.Read(stored.Content)?.GetProperty("name").GetString());

        Assert.Equal(["message.created"], _notifier.Events);
    }

    private ChatwootEventHandler Handler()
        => new(
            _conversations,
            _store,
            _notifier,
            new RecordingRealTimePublisher(),
            _clock);

    private static ChatwootEvent StaffSays(string conversationId)
        => new(
            "message_created",
            conversationId,
            ChatwootConversationId: 1,
            Status: "open",
            AssigneeName: "Dana R.",
            MessageType: "outgoing",
            IsPrivate: false,
            Content: Reply,
            ActorType: ChatwootEvent.StaffActor,
            ActorId: 7,
            ActorName: "Dana R.");

    private static ChatwootEvent AssignedTo(string conversationId, string agent)
        => new(
            "conversation_updated",
            conversationId,
            ChatwootConversationId: 1,
            Status: "open",
            AssigneeName: agent,
            MessageType: null,
            IsPrivate: false,
            Content: null,
            ActorType: null,
            ActorId: null,
            ActorName: null);

    /// <summary>A visitor's chat that asked for a person and was taken by Dana.</summary>
    private async Task<string> TakenChatAsync()
    {
        var conversationId = Guid.NewGuid().ToString("N");
        await _conversations.CreateAsync(conversationId, Cancel);

        await _store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel);
        await _store.ClaimAsync(conversationId, "chatwoot:Dana R.", "Dana R.", Cancel);

        return conversationId;
    }
}
