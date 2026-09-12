using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

using SpiritAI.Handoffs;
using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Mail;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.PublicChat;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.RealTime;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Desk;

/// <summary>
/// The desk over its fakes: what the routes and the bot's tool share.
/// </summary>
public sealed class HandoffDeskTests
{
    private const string VisitorKey = "v1";

    private static readonly HandoffStaffMember Dana = new("dana@example.com", "Dana R.");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeHandoffStore _store;
    private readonly ICallStore _calls;
    private readonly RecordingHandoffNotifier _notifier = new();
    private readonly FakePresenceStore _presence;
    private readonly RecordingHandoffMailer _mailer = new();
    private readonly HandoffDesk _desk;

    public HandoffDeskTests()
    {
        _store = new FakeHandoffStore(_clock);
        _calls = new InMemoryCallStore(_clock);
        _presence = new FakePresenceStore(_clock, TimeSpan.FromSeconds(90));
        _desk = Desk(_mailer);
    }

    [Fact]
    public async Task AnnouncingTheQueueTellsEachWaitingChatWhereItStandsNow()
    {
        var first = await AskAsync();
        var second = await AskAsync();
        var third = await AskAsync();
        await _store.DoneAsync(first, Cancel);

        await _desk.AnnounceQueueAsync(Cancel);

        Assert.Equal(
            [("handoff.queue", (second, 1)), ("handoff.queue", (third, 2))],
            _notifier.Pushed.Where(push => push.Event == "handoff.queue").Select(push => (push.Event, ((string, int))push.Payload)));
    }

    [Fact]
    public async Task TheVisitorCannotSpeakToAChatTheBotHas()
    {
        var callId = Guid.NewGuid().ToString("N");
        await _calls.CreateAsync(callId, Cancel);

        var said = await _desk.VisitorSaysAsync(callId, "Still there?", Cancel);

        Assert.Null(said);
        Assert.Empty(await _calls.ReadAsync(callId, Cancel));
        Assert.Empty(_notifier.Events);
    }

    [Fact]
    public async Task AReplyIsMailedWhenTheVisitorIsAwayAndLeftAnEmail()
    {
        var open = await TakenChatAsync(email: "pat@example.com");

        var created = await _desk.StaffSaysAsync(open, Dana, "Try the tension bolt.", Cancel);

        var mail = Assert.Single(_mailer.Sent);
        Assert.Equal("pat@example.com", mail.To);
        Assert.Equal(open.CallId, mail.CallId);
        Assert.Equal("Dana R.", mail.StaffName);
        Assert.Equal("Try the tension bolt.", mail.Text);

        await AssertStoredAndPushedAsync(open.CallId, created);
    }

    [Fact]
    public async Task NoMailWhileTheVisitorIsOnline()
    {
        var open = await TakenChatAsync(email: "pat@example.com");
        await _presence.ConnectAsync(
            "socket-1", VisitorPrincipal.KeyOf(VisitorKey), name: null, HandoffAdmission.VisitorKind, Cancel);

        var created = await _desk.StaffSaysAsync(open, Dana, "Try the tension bolt.", Cancel);

        Assert.Empty(_mailer.Sent);
        await AssertStoredAndPushedAsync(open.CallId, created);
    }

    [Fact]
    public async Task NoMailWithoutAnEmail()
    {
        var open = await TakenChatAsync(email: null);

        var created = await _desk.StaffSaysAsync(open, Dana, "Try the tension bolt.", Cancel);

        Assert.Empty(_mailer.Sent);
        await AssertStoredAndPushedAsync(open.CallId, created);
    }

    [Fact]
    public async Task AFailedSendNeverFailsTheReply()
    {
        var open = await TakenChatAsync(email: "pat@example.com");

        var desk = Desk(new ThrowingHandoffMailer());

        var created = await desk.StaffSaysAsync(open, Dana, "Try the tension bolt.", Cancel);

        Assert.Equal("Try the tension bolt.", created.Text);
        await AssertStoredAndPushedAsync(open.CallId, created);
    }

    /// <summary>The reply is in the chat, signed, and was pushed as the message returned.</summary>
    private async Task AssertStoredAndPushedAsync(string callId, HandoffMessage created)
    {
        var stored = Assert.Single(await _calls.ReadAsync(callId, Cancel));
        Assert.Equal(created.MessageId, stored.MessageId);
        Assert.Equal(ChatRole.Assistant, stored.Content.Role);
        Assert.Equal(created.Text, stored.Content.Text);
        Assert.Equal("Dana R.", SpeakerProperty.Read(stored.Content)?.GetProperty("name").GetString());

        Assert.Equal("assistant", created.Role);
        Assert.Equal("human", created.Speaker?.Kind);
        Assert.Contains(("message.created", (object)created), _notifier.Pushed);
    }

    private HandoffDesk Desk(IHandoffMailer mailer)
        => new(_store, _calls, _notifier, _presence, mailer, _clock, NullLogger<HandoffDesk>.Instance);

    /// <summary>Asks for a person on a new chat, a minute after the last ask, so the line has an order.</summary>
    private async Task<string> AskAsync()
    {
        var callId = Guid.NewGuid().ToString("N");
        await _calls.CreateAsync(callId, Cancel);

        _clock.Now += TimeSpan.FromMinutes(1);
        await _store.AskAsync(callId, HandoffAskedBy.Visitor, null, Cancel);

        return callId;
    }

    /// <summary>A visitor's chat that asked, was taken by Dana, and may have an email on it.</summary>
    private async Task<Handoff> TakenChatAsync(string? email)
    {
        var callId = await AskAsync();
        await _calls.SetCustomAsync(callId, ThreadEnvelope.Build(VisitorPrincipal.KeyOf(VisitorKey), null), Cancel);
        await _store.ClaimAsync(callId, "user_dana", Dana.Name, Cancel);

        if (email is not null)
        {
            await _store.SetEmailAsync(callId, email, Cancel);
        }

        return (await _store.OpenAsync(callId, Cancel))!;
    }
}
