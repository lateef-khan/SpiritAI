
using AgentCore.Application.Ports;
using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Contacts;
using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.PublicChat;
using SpiritAI.RealTime;
using SpiritAI.RealTime.Presence;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Contacts;
using SpiritAI.Tests.RealTime;
using SpiritAI.Tests.Threads;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.RealTime;

/// <summary>
/// The visitor's browser on the real hub with the real admission. Staff work in Chatwoot, and the
/// Chatwoot webhook reaches the visitor through the publisher: what the widget relies on is that
/// a staff typing signal and a message push both reach the chat's visitor.
/// </summary>
public sealed class HandoffSocketTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-16T12:00:00Z", null);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly List<HubConnection> _connections = [];
    private IHost? _host;
    private NeonAuthTestKit? _kit;
    private IConversations? _conversations;
    private FakeContactResolver? _contacts;
    private FakeContactConversationStore? _contactConversations;

    [Fact]
    public async Task AStaffTypingSignalReachesTheVisitor()
    {
        var conversationId = await StartAsync();
        var visitor = Visitor(conversationId);
        var toVisitor = new Inbox<RealTimeSignal>(visitor, RealTimeEvents.Signal);
        Assert.True(await SpiritHubWorld.AdmittedAsync(visitor), "the visitor was not admitted");

        var group = HandoffGroups.ForConversation(conversationId);
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new { callId = conversationId, on = true });
        var signal = new RealTimeSignal(new RealTimeSender("chatwoot:7", HandoffAdmission.StaffKind), group, "typing", payload);
        await _host!.Services.GetRequiredService<IRealTimePublisher>().PublishAsync(group, RealTimeEvents.Signal, signal, Cancel);

        var heard = await toVisitor.NextAsync();
        Assert.Equal(HandoffAdmission.StaffKind, heard.Sender.Kind);
        Assert.Equal("typing", heard.Name);
        Assert.True(heard.Payload.GetProperty("on").GetBoolean());
    }

    [Fact]
    public async Task AMessagePushReachesTheChatsVisitor()
    {
        var conversationId = await StartAsync();
        var visitor = Visitor(conversationId);
        var toVisitor = new Inbox<HandoffMessage>(visitor, HandoffEvents.MessageCreated);
        Assert.True(await SpiritHubWorld.AdmittedAsync(visitor));

        var notifier = _host!.Services.GetRequiredService<IHandoffNotifier>();
        var message = new HandoffMessage(conversationId, "m-1", "assistant", "Hi, Dana here.", HandoffSpeaker.Human("Dana R.", "Support"), Start);
        await notifier.MessageCreatedAsync(message, Cancel);

        Assert.Equal("Hi, Dana here.", (await toVisitor.NextAsync()).Text);
    }

    /// <summary>Starts the host with one chat owned by the visitor, and answers its id.</summary>
    private async Task<string> StartAsync()
    {
        _kit = new NeonAuthTestKit();
        TestTimeProvider clock = new(Start);
        _conversations = new Conversations(new InMemoryConversationStore(clock), blobs: null);
        _contacts = new FakeContactResolver();
        _contactConversations = new FakeContactConversationStore();

        _host = await ThreadTestHost.StartAsync(
            _kit,
            services =>
            {
                services.AddRealTime(new ConfigurationBuilder().Build());
                services.AddHandoffRealTime();
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton(_conversations);
                services.AddSingleton<IContactResolver>(_contacts);
                services.AddSingleton<IContactConversationStore>(_contactConversations);
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

        var conversationId = Guid.NewGuid().ToString("N");
        var key = VisitorPrincipal.KeyOf(VisitorKey);

        await _conversations.CreateAsync(conversationId, Cancel);
        await _conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(key, app: null), Cancel);

        var contactId = await _contacts.ResolveAsync(key, Cancel);
        await _contactConversations.EnsureAsync(conversationId, contactId, ContactChannel.Chat, Cancel);

        return conversationId;
    }

    private const string VisitorKey = "visitor-abc";

    /// <summary>The visitor's socket, naming the chat and their key.</summary>
    private HubConnection Visitor(string conversationId)
        => Connect($"{HandoffAdmission.ConversationQuery}={conversationId}&{HandoffAdmission.VisitorQuery}={VisitorKey}");

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
