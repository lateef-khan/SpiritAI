using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;

using Microsoft.Extensions.Logging.Abstractions;

using SpiritAI.Handoffs.Bot;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.RealTime;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>
/// The bot's door into the queue, section 9.4 of the handoff spec.
/// </summary>
public sealed class RequestHumanToolTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeHandoffStore _store;
    private readonly ICallStore _calls;
    private readonly RecordingHandoffNotifier _notifier = new();
    private readonly FakePresenceStore _presence;
    private readonly RequestHumanTool _tool;

    public RequestHumanToolTests()
    {
        _store = new FakeHandoffStore(_clock);
        _calls = new InMemoryCallStore(_clock);
        _presence = new FakePresenceStore(_clock, TimeSpan.FromSeconds(90));
        _tool = new RequestHumanTool(
            new HandoffDesk(
                _store,
                _calls,
                _notifier,
                _presence,
                new RecordingHandoffMailer(),
                _clock,
                NullLogger<HandoffDesk>.Instance));
    }

    [Fact]
    public async Task InsideAChatTheToolJoinsTheQueueAndSaysWhere()
    {
        var callId = Guid.NewGuid().ToString("N");
        await _calls.CreateAsync(callId, Cancel);
        await _presence.ConnectAsync("socket-1", "user:dana", "Dana R.", HandoffAdmission.StaffKind, Cancel);

        var answer = await _tool.AskAsync(callId, "they want a real person", Cancel);

        Assert.Equal(1, answer.Position);
        Assert.Equal(1, answer.StaffOnline);
        Assert.Equal(RequestHumanTool.AskedNote, answer.Note);

        var row = Assert.Single(_store.Rows);
        Assert.Equal(callId, row.CallId);
        Assert.Equal(HandoffStatus.Waiting, row.Status);
        Assert.Equal(HandoffAskedBy.Bot, row.AskedBy);
        Assert.Equal("they want a real person", row.Reason);
        Assert.Equal(["handoff.waiting"], _notifier.Events);
    }

    [Fact]
    public async Task WithNobodyOnlineTheToolSaysToOfferAnEmail()
    {
        var callId = Guid.NewGuid().ToString("N");
        await _calls.CreateAsync(callId, Cancel);

        var answer = await _tool.AskAsync(callId, "they want a real person", Cancel);

        Assert.Equal(0, answer.StaffOnline);
        Assert.Equal(RequestHumanTool.NobodyFreeNote, answer.Note);
    }
}
