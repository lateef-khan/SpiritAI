using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Mail;
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
/// A staff reply from Chatwoot reaching the visitor: stored in the chat, signed, pushed, and mailed
/// when the visitor is away and left an email.
/// </summary>
public sealed class ChatwootEventHandlerTests
{
    private const string VisitorKey = "v1";
    private const string Reply = "Try the tension bolt.";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeHandoffStore _store;
    private readonly IConversations _conversations;
    private readonly RecordingHandoffNotifier _notifier = new();
    private readonly FakePresenceStore _presence;
    private readonly RecordingHandoffMailer _mailer = new();

    public ChatwootEventHandlerTests()
    {
        _store = new FakeHandoffStore(_clock);
        _conversations = new Conversations(new InMemoryConversationStore(_clock), blobs: null);
        _presence = new FakePresenceStore(_clock, TimeSpan.FromSeconds(90));
    }

    [Fact]
    public async Task AReplyIsMailedWhenTheVisitorIsAwayAndLeftAnEmail()
    {
        var conversationId = await TakenChatAsync(email: "pat@example.com");

        await Handler(_mailer).HandleAsync(StaffSays(conversationId), Cancel);

        var mail = Assert.Single(_mailer.Sent);
        Assert.Equal(("pat@example.com", conversationId, "Dana R.", Reply), (mail.To, mail.ConversationId, mail.StaffName, mail.Text));
        await AssertStoredAndPushedAsync(conversationId);
    }

    [Fact]
    public async Task NoMailWhileTheVisitorIsOnline()
    {
        var conversationId = await TakenChatAsync(email: "pat@example.com");
        await _presence.ConnectAsync("socket-1", VisitorPrincipal.KeyOf(VisitorKey), name: null, HandoffAdmission.VisitorKind, Cancel);

        await Handler(_mailer).HandleAsync(StaffSays(conversationId), Cancel);

        Assert.Empty(_mailer.Sent);
        await AssertStoredAndPushedAsync(conversationId);
    }

    [Fact]
    public async Task NoMailWithoutAnEmail()
    {
        var conversationId = await TakenChatAsync(email: null);

        await Handler(_mailer).HandleAsync(StaffSays(conversationId), Cancel);

        Assert.Empty(_mailer.Sent);
        await AssertStoredAndPushedAsync(conversationId);
    }

    [Fact]
    public async Task AFailedSendNeverFailsTheReply()
    {
        var conversationId = await TakenChatAsync(email: "pat@example.com");

        await Handler(new ThrowingHandoffMailer()).HandleAsync(StaffSays(conversationId), Cancel);

        await AssertStoredAndPushedAsync(conversationId);
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

    private ChatwootEventHandler Handler(IHandoffMailer mailer)
        => new(
            _conversations,
            _store,
            _notifier,
            new RecordingRealTimePublisher(),
            _presence,
            mailer,
            _clock,
            NullLogger<ChatwootEventHandler>.Instance);

    private static ChatwootEvent StaffSays(string conversationId)
        => new(
            "message_created",
            conversationId,
            ChatwootConversationId: 1,
            Status: "open",
            AssigneeName: "Dana R.",
            ContactEmail: null,
            MessageType: "outgoing",
            IsPrivate: false,
            Content: Reply,
            ActorType: ChatwootEvent.StaffActor,
            ActorId: 7,
            ActorName: "Dana R.");

    /// <summary>A visitor's chat that asked for a person, was taken by Dana, and may have an email on it.</summary>
    private async Task<string> TakenChatAsync(string? email)
    {
        var conversationId = Guid.NewGuid().ToString("N");
        await _conversations.CreateAsync(conversationId, Cancel);
        await _conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(VisitorPrincipal.KeyOf(VisitorKey), null), Cancel);

        await _store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel);
        await _store.ClaimAsync(conversationId, "chatwoot:Dana R.", "Dana R.", Cancel);

        if (email is not null)
        {
            await _store.SetEmailAsync(conversationId, email, Cancel);
        }

        return conversationId;
    }
}
