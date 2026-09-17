
using AgentCore.Application.Ports;
using AgentCore.Application.Calls.Memory;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Auth.Users;
using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Staff;
using SpiritAI.PublicChat;
using SpiritAI.RealTime;
using SpiritAI.RealTime.Presence;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Auth.Users;
using SpiritAI.Tests.RealTime;
using SpiritAI.Tests.Threads;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.RealTime;

/// <summary>
/// The two browsers of a handoff on the real hub with the real admission: a member of staff who
/// sends a token the way a browser socket can, and a visitor who names their chat. What the
/// inbox and the widget rely on is that a typing signal crosses from one to the other, and that a
/// message push reaches both.
/// </summary>
public sealed class HandoffSocketTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-16T12:00:00Z", null);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly List<HubConnection> _connections = [];
    private IHost? _host;
    private NeonAuthTestKit? _kit;
    private ICallStore? _calls;

    [Fact]
    public async Task AVisitorsTypingReachesStaffAndTheirsReachesTheVisitor()
    {
        var callId = await StartAsync();
        var staff = Staff();
        var visitor = Visitor(callId);
        var toStaff = new Inbox<RealTimeSignal>(staff, RealTimeEvents.Signal);
        var toVisitor = new Inbox<RealTimeSignal>(visitor, RealTimeEvents.Signal);
        Assert.True(await SpiritHubWorld.AdmittedAsync(staff), "staff was not admitted");
        Assert.True(await SpiritHubWorld.AdmittedAsync(visitor), "the visitor was not admitted");

        await visitor.InvokeAsync(nameof(SpiritHub.Signal), HandoffGroups.Staff, "typing", new { callId, on = true }, Cancel);

        var heardByStaff = await toStaff.NextAsync();
        Assert.Equal(HandoffAdmission.VisitorKind, heardByStaff.Sender.Kind);
        Assert.Equal("typing", heardByStaff.Name);
        Assert.Equal(callId, heardByStaff.Payload.GetProperty("callId").GetString());
        Assert.True(heardByStaff.Payload.GetProperty("on").GetBoolean());

        await staff.InvokeAsync(nameof(SpiritHub.Signal), HandoffGroups.ForCall(callId), "typing", new { callId, on = true }, Cancel);

        var heardByVisitor = await toVisitor.NextAsync();
        Assert.Equal(HandoffAdmission.StaffKind, heardByVisitor.Sender.Kind);
        Assert.Equal(HandoffGroups.ForCall(callId), heardByVisitor.Group);
    }

    [Fact]
    public async Task AMessagePushReachesStaffAndTheChatsVisitor()
    {
        var callId = await StartAsync();
        var staff = Staff();
        var visitor = Visitor(callId);
        var toStaff = new Inbox<HandoffMessage>(staff, HandoffEvents.MessageCreated);
        var toVisitor = new Inbox<HandoffMessage>(visitor, HandoffEvents.MessageCreated);
        Assert.True(await SpiritHubWorld.AdmittedAsync(staff));
        Assert.True(await SpiritHubWorld.AdmittedAsync(visitor));

        var notifier = _host!.Services.GetRequiredService<IHandoffNotifier>();
        var message = new HandoffMessage(callId, "m-1", "assistant", "Dana R. joined", HandoffSpeaker.System(), Start);
        await notifier.MessageCreatedAsync(message, Cancel);

        Assert.Equal("Dana R. joined", (await toStaff.NextAsync()).Text);
        Assert.Equal("Dana R. joined", (await toVisitor.NextAsync()).Text);
    }

    /// <summary>Starts the host with one chat owned by the visitor, and answers its id.</summary>
    private async Task<string> StartAsync()
    {
        _kit = new NeonAuthTestKit();
        TestTimeProvider clock = new(Start);
        _calls = new InMemoryCallStore(clock);

        _host = await ThreadTestHost.StartAsync(
            _kit,
            services =>
            {
                services.AddRealTime(new ConfigurationBuilder().Build());
                services.AddHandoffRealTime();
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton(_calls);
                services.AddSingleton<IUserDirectory>(new FakeUserDirectory(new AuthUser("user_dana", "Dana Rivera", "dana@example.com")));
                services.AddScoped<StaffGate>();
                services.AddSingleton<IPresenceStore>(new FakePresenceStore(clock, TimeSpan.FromSeconds(90)));
            },
            app =>
            {
                app.UseNeonAuthOnApi();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapRealTime());
            },
            auth =>
            {
                auth.OpenPathPrefixes = [SpiritHub.Pattern];
                auth.QueryTokenPathPrefixes = [SpiritHub.Pattern];
            });

        var callId = Guid.NewGuid().ToString("N");
        await _calls.CreateAsync(callId, Cancel);
        await _calls.SetCustomAsync(callId, ThreadEnvelope.Build(VisitorPrincipal.KeyOf(VisitorKey), app: null), Cancel);

        return callId;
    }

    private const string VisitorKey = "visitor-abc";

    /// <summary>A member of staff's socket, the token in the query the way a browser sends it.</summary>
    private HubConnection Staff()
        => Connect($"access_token={_kit!.Token(subject: "user_dana", email: "dana@example.com")}");

    /// <summary>The visitor's socket, naming the chat and their key.</summary>
    private HubConnection Visitor(string callId)
        => Connect($"{HandoffAdmission.CallQuery}={callId}&{HandoffAdmission.VisitorQuery}={VisitorKey}");

    private HubConnection Connect(string query)
    {
        var server = _host!.GetTestServer();
        var url = new Uri(server.BaseAddress, $"{SpiritHub.Pattern}?{query}");

        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var client = server.CreateWebSocketClient();
                    return await client.ConnectAsync(context.Uri, cancellationToken);
                };
            })
            .Build();

        _connections.Add(connection);
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
