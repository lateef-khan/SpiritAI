using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.RealTime;
using SpiritAI.RealTime.Presence;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.RealTime;

using Xunit;

namespace SpiritAI.Tests.Handoffs.RealTime;

/// <summary>
/// Keeps the widget's "someone is online" dot current. Chatwoot says nothing when an agent opens or
/// closes the dashboard, so the count is read on a timer, and only while a widget waits for a person
/// or is with one: that is when a widget has a socket to hear it.
/// </summary>
public sealed class StaffPresenceWatchTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 9, 23, 9, 0, 0, TimeSpan.Zero));
    private readonly FakePresenceStore _sockets;
    private readonly FakeStaffPresence _staff = new();
    private readonly RecordingRealTimePublisher _publisher = new();
    private readonly StaffPresenceWatch _watch;

    public StaffPresenceWatchTests()
    {
        _sockets = new FakePresenceStore(_clock, TimeSpan.FromSeconds(90));

        var scopes = new ServiceCollection()
            .AddSingleton<IPresenceStore>(_sockets)
            .AddSingleton<IStaffPresence>(_staff)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        _watch = new StaffPresenceWatch(scopes, _publisher, NullLogger<StaffPresenceWatch>.Instance);
    }

    [Fact]
    public async Task WithNoWidgetWaitingChatwootIsNotAsked()
    {
        _staff.Online = 1;

        await _watch.CheckAsync(Cancel);

        Assert.Equal(0, _staff.Reads);
        Assert.Empty(_publisher.Pushed);
    }

    [Fact]
    public async Task AWaitingWidgetHearsHowManyStaffAreOnline()
    {
        await _sockets.ConnectAsync("socket-1", "visitor:pat", name: null, HandoffAdmission.VisitorKind, Cancel);
        _staff.Online = 2;

        await _watch.CheckAsync(Cancel);

        var push = Assert.Single(_publisher.Pushed);
        Assert.Equal([RealTimeGroups.Presence], push.Groups);
        Assert.Equal(RealTimeEvents.Presence, push.Event);
        Assert.Equal(new RealTimePresence(HandoffAdmission.StaffKind, 2), push.Payload);
    }

    [Fact]
    public async Task OnlyAChangedCountIsPushedAgain()
    {
        await _sockets.ConnectAsync("socket-1", "visitor:pat", name: null, HandoffAdmission.VisitorKind, Cancel);
        _staff.Online = 1;

        await _watch.CheckAsync(Cancel);
        await _watch.CheckAsync(Cancel);

        _staff.Online = 0;
        await _watch.CheckAsync(Cancel);

        Assert.Equal(
            [new RealTimePresence(HandoffAdmission.StaffKind, 1), new RealTimePresence(HandoffAdmission.StaffKind, 0)],
            _publisher.Pushed.Select(p => p.Payload));
    }
}
