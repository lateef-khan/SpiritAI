using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.RealTime;
using SpiritAI.Tests.RealTime;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Notifications;

/// <summary>
/// The table of section 6.2 of the handoff spec: each push, the groups it goes to, and what it
/// carries, read off the wire the way the browser would.
/// </summary>
public sealed class HandoffNotifierTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly RecordingRealTimePublisher _publisher = new();

    private readonly IHandoffNotifier _notifier;

    public HandoffNotifierTests()
    {
        // Through the registration, so the test also proves AddHandoffRealTime puts this notifier in place.
        var services = new ServiceCollection()
            .AddSingleton<IRealTimePublisher>(_publisher)
            .AddHandoffRealTime()
            .BuildServiceProvider();

        _notifier = services.GetRequiredService<IHandoffNotifier>();
    }

    [Fact]
    public async Task WaitingGoesToStaff()
    {
        var summary = new HandoffSummary(1, "c1", "waiting", "visitor", null, At, null, null, null, null, "Title", "Hello?", 1);

        await _notifier.WaitingAsync(summary, Cancel);

        var (groups, name, payload) = Assert.Single(_publisher.Pushed);
        Assert.Equal([HandoffGroups.Staff], groups);
        Assert.Equal(HandoffEvents.Waiting, name);
        Assert.Same(summary, payload);
    }

    [Fact]
    public async Task QueueGoesToThatChat()
    {
        await _notifier.QueueAsync("c1", 3, Cancel);

        var (groups, name, payload) = Assert.Single(_publisher.Pushed);
        Assert.Equal(["call:c1"], groups);
        Assert.Equal(HandoffEvents.Queue, name);
        Assert.Equal(new HandoffQueuePosition("c1", 3), payload);
    }

    [Fact]
    public async Task ClaimedGoesToStaffAndThatChat()
    {
        await _notifier.ClaimedAsync("c1", new HandoffAssignee("user:dana", "Dana R."), Cancel);

        var (groups, name, payload) = Assert.Single(_publisher.Pushed);
        Assert.Equal([HandoffGroups.Staff, "call:c1"], groups);
        Assert.Equal(HandoffEvents.Claimed, name);
        var wire = Wire(payload);
        Assert.Equal("c1", wire.GetProperty("callId").GetString());
        Assert.Equal("Dana R.", wire.GetProperty("assignee").GetProperty("name").GetString());
    }

    [Fact]
    public async Task DoneGoesToStaffAndThatChat()
    {
        await _notifier.DoneAsync("c1", Cancel);

        var (groups, name, payload) = Assert.Single(_publisher.Pushed);
        Assert.Equal([HandoffGroups.Staff, "call:c1"], groups);
        Assert.Equal(HandoffEvents.Done, name);
        Assert.Equal("c1", Wire(payload).GetProperty("callId").GetString());
    }

    [Fact]
    public async Task MessageCreatedGoesToStaffAndTheMessagesChat()
    {
        var message = new HandoffMessage("c1", "m7", "assistant", "On my way.", HandoffSpeaker.Human("Dana R.", "Support"), At);

        await _notifier.MessageCreatedAsync(message, Cancel);

        var (groups, name, payload) = Assert.Single(_publisher.Pushed);
        Assert.Equal([HandoffGroups.Staff, "call:c1"], groups);
        Assert.Equal(HandoffEvents.MessageCreated, name);
        Assert.Same(message, payload);
    }

    /// <summary>The payload as JSON, spelled the way SignalR spells it: camelCase.</summary>
    private static JsonElement Wire(object payload)
        => JsonSerializer.SerializeToElement(payload, JsonSerializerOptions.Web);
}
