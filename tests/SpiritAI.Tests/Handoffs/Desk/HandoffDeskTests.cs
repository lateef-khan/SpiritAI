using AgentCore.Application.Calls.Memory;

using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.RealTime;

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
    private readonly InMemoryCallStore _calls;
    private readonly RecordingHandoffTranscript _transcript = new();
    private readonly RecordingHandoffNotifier _notifier = new();
    private readonly HandoffDesk _desk;

    public HandoffDeskTests()
    {
        _store = new FakeHandoffStore(_clock);
        _calls = new InMemoryCallStore(_clock);
        _desk = new HandoffDesk(
            _store,
            _calls,
            _transcript,
            _notifier,
            new FakePresenceStore(_clock, TimeSpan.FromSeconds(90)),
            _clock);
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
        Assert.Empty(_transcript.Appended);
        Assert.Empty(_notifier.Events);
    }

    /// <summary>Asks for a person on a new chat, a minute after the last ask, so the line has an order.</summary>
    private async Task<string> AskAsync()
    {
        var callId = Guid.NewGuid().ToString("N");
        await _calls.CreateAsync(callId, Cancel);

        _clock.Now += TimeSpan.FromMinutes(1);
        await _store.AskAsync(callId, HandoffAskedBy.Visitor, null, Cancel);

        return callId;
    }
}
