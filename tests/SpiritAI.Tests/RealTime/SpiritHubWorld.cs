using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.RealTime;
using SpiritAI.RealTime.Presence;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Threads;

using Xunit;

namespace SpiritAI.Tests.RealTime;

/// <summary>
/// The hub on a test server: a real token check, the real hub and publisher, one fake admission,
/// and a fake presence store. Clients connect over the test server's own WebSocket, never a port.
/// </summary>
internal sealed class SpiritHubWorld : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly IHost _host;
    private readonly NeonAuthTestKit _kit;
    private readonly List<HubConnection> _connections = [];

    private SpiritHubWorld(IHost host, NeonAuthTestKit kit, FakeAdmission admission, FakePresenceStore presence, TestTimeProvider clock)
    {
        _host = host;
        _kit = kit;
        Admission = admission;
        Presence = presence;
        Clock = clock;
        Publisher = host.Services.GetRequiredService<IRealTimePublisher>();
    }

    public FakeAdmission Admission { get; }

    public FakePresenceStore Presence { get; }

    public TestTimeProvider Clock { get; }

    /// <summary>The real one, over the hub: what a feature pushes through.</summary>
    public IRealTimePublisher Publisher { get; }

    public static async Task<SpiritHubWorld> StartAsync()
    {
        var kit = new NeonAuthTestKit();
        TestTimeProvider clock = new(Start);
        FakeAdmission admission = new();
        FakePresenceStore presence = new(clock, TimeSpan.FromSeconds(90));

        var host = await ThreadTestHost.StartAsync(
            kit,
            services =>
            {
                services.AddRealTime(new ConfigurationBuilder().Build());
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IRealTimeAdmission>(admission);
                services.AddSingleton<IPresenceStore>(presence);
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

        return new SpiritHubWorld(host, kit, admission, presence, clock);
    }

    /// <summary>A socket the fake admission knows by name.</summary>
    public HubConnection Connect(string @as) => ConnectWith($"as={@as}");

    /// <summary>A socket that says nothing about who it is.</summary>
    public HubConnection Nobody() => ConnectWith(query: null);

    /// <summary>A socket carrying a token that will not verify, the way a browser sends one.</summary>
    public HubConnection BadToken(string @as)
        => ConnectWith($"as={@as}&access_token={_kit.Token(corruptSignature: true)}");

    /// <summary>
    /// Whether the hub let a socket stay. The handshake succeeds before the hub looks at who is
    /// calling, so a refusal shows as the socket closing right after; a round trip proves the
    /// other outcome.
    /// </summary>
    public static async Task<bool> AdmittedAsync(HubConnection connection)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);
            await connection.InvokeAsync(nameof(SpiritHub.Heartbeat), TestContext.Current.CancellationToken);
        }
        catch (Exception)
        {
            await closed.Task.WaitAsync(Inbox<object>.Patience, TestContext.Current.CancellationToken);
            return false;
        }

        return connection.State == HubConnectionState.Connected;
    }

    private HubConnection ConnectWith(string? query)
    {
        var server = _host.GetTestServer();
        var url = new Uri(server.BaseAddress, SpiritHub.Pattern + (query is null ? string.Empty : "?" + query));

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

        await _host.StopAsync(TestContext.Current.CancellationToken);
        _host.Dispose();
    }
}
