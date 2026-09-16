using System.Text.Json;

using Microsoft.AspNetCore.SignalR.Client;

using SpiritAI.RealTime;

using Xunit;

namespace SpiritAI.Tests.RealTime;

/// <summary>
/// Sections 6.1 to 6.4 of the handoff spec, restated for the one door every feature shares: who
/// the hub admits, which group a push reaches, and what a socket may say.
/// </summary>
public sealed class SpiritHubTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APublishReachesItsGroupAndNoOther()
    {
        await using var world = await SpiritHubWorld.StartAsync();
        var (alpha, beta) = (world.Connect("alpha"), world.Connect("beta"));
        var toAlpha = new Inbox<JsonElement>(alpha, "thing.happened");
        var toBeta = new Inbox<JsonElement>(beta, "thing.happened");
        Assert.True(await SpiritHubWorld.AdmittedAsync(alpha));
        Assert.True(await SpiritHubWorld.AdmittedAsync(beta));

        await world.Publisher.PublishAsync(FakeAdmission.RoomA, "thing.happened", new { text = "hello" }, Cancel);

        Assert.Equal("hello", (await toAlpha.NextAsync()).GetProperty("text").GetString());
        Assert.True(await toBeta.StaysEmptyAsync());
    }

    [Fact]
    public async Task NobodyAdmittedIsRefused()
    {
        await using var world = await SpiritHubWorld.StartAsync();

        Assert.False(await SpiritHubWorld.AdmittedAsync(world.Connect("gamma")));
        Assert.False(await SpiritHubWorld.AdmittedAsync(world.Nobody()));
        Assert.Empty(world.Presence.Rows);
    }

    [Fact]
    public async Task ABadTokenIsRefusedBeforeAnyAdmissionIsAsked()
    {
        await using var world = await SpiritHubWorld.StartAsync();

        Assert.False(await SpiritHubWorld.AdmittedAsync(world.BadToken("alpha")));
        Assert.Equal(0, world.Admission.Asked);
        Assert.Empty(world.Presence.Rows);
    }

    [Fact]
    public async Task ASignalCarriesItsSenderToAnAllowedGroupOnly()
    {
        await using var world = await SpiritHubWorld.StartAsync();
        var (alpha, beta) = (world.Connect("alpha"), world.Connect("beta"));
        var toAlpha = new Inbox<RealTimeSignal>(alpha, RealTimeEvents.Signal);
        var toBeta = new Inbox<RealTimeSignal>(beta, RealTimeEvents.Signal);
        Assert.True(await SpiritHubWorld.AdmittedAsync(alpha));
        Assert.True(await SpiritHubWorld.AdmittedAsync(beta));

        await alpha.InvokeAsync(nameof(SpiritHub.Signal), FakeAdmission.RoomB, "typing", new { on = true }, Cancel);

        var signal = await toBeta.NextAsync();
        Assert.Equal(new RealTimeSender(FakeAdmission.AlphaKey, "alpha"), signal.Sender);
        Assert.Equal(FakeAdmission.RoomB, signal.Group);
        Assert.Equal("typing", signal.Name);
        Assert.True(signal.Payload.GetProperty("on").GetBoolean());

        await alpha.InvokeAsync(nameof(SpiritHub.Signal), FakeAdmission.RoomA, "typing", new { on = true }, Cancel);
        await beta.InvokeAsync(nameof(SpiritHub.Signal), FakeAdmission.RoomA, "typing", new { on = true }, Cancel);

        Assert.True(await toAlpha.StaysEmptyAsync());
        Assert.True(await toBeta.StaysEmptyAsync());
    }

    [Fact]
    public async Task HeartbeatTouchesPresence()
    {
        await using var world = await SpiritHubWorld.StartAsync();
        var alpha = world.Connect("alpha");
        await alpha.StartAsync(Cancel);
        world.Clock.Now += TimeSpan.FromSeconds(30);

        await alpha.InvokeAsync(nameof(SpiritHub.Heartbeat), Cancel);

        var touched = Assert.Single(world.Presence.Touches);
        var row = Assert.Single(world.Presence.Rows, r => r.ConnectionId == touched);
        Assert.Equal(world.Clock.Now, row.SeenAt);
        Assert.Equal(FakeAdmission.AlphaKey, row.CallerKey);
        Assert.Equal("alpha", row.Kind);
    }

    [Fact]
    public async Task ConnectAndDisconnectAnnounceTheCountOfThatKind()
    {
        await using var world = await SpiritHubWorld.StartAsync();
        var (alpha, beta) = (world.Connect("alpha"), world.Connect("beta"));
        var presence = new Inbox<RealTimePresence>(alpha, RealTimeEvents.Presence);

        await alpha.StartAsync(Cancel);
        Assert.Equal(new RealTimePresence("alpha", 1), await presence.NextAsync());

        await beta.StartAsync(Cancel);
        Assert.Equal(new RealTimePresence("beta", 1), await presence.NextAsync());

        await beta.StopAsync(Cancel);
        Assert.Equal(new RealTimePresence("beta", 0), await presence.NextAsync());
        Assert.Equal("alpha", Assert.Single(world.Presence.Rows).Kind);
    }
}
