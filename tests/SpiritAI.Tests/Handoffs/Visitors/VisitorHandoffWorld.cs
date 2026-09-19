using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Mail;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Visitors;
using SpiritAI.PublicChat;
using SpiritAI.RealTime.Presence;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.PublicChat;
using SpiritAI.Tests.RealTime;
using SpiritAI.Tests.Threads;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Visitors;

/// <summary>
/// The visitor's handoff routes on a test server: the real token check leaving them open, the
/// real desk, the in-memory conversation store, fakes for every other port under it, and the three widgets
/// a test needs.
/// </summary>
internal sealed class VisitorHandoffWorld : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly IHost _host;

    private VisitorHandoffWorld(
        IHost host,
        FakeHandoffStore store,
        IConversations conversations,
        RecordingHandoffNotifier notifier,
        FakePresenceStore presence,
        TestTimeProvider clock)
    {
        _host = host;
        Store = store;
        Conversations = conversations;
        Notifier = notifier;
        Presence = presence;
        Clock = clock;
        Visitor = new VisitorCaller(host.GetTestClient(), "widget-one");
        Stranger = new VisitorCaller(host.GetTestClient(), "widget-two");
        Anonymous = new VisitorCaller(host.GetTestClient(), visitorKey: null);
    }

    public FakeHandoffStore Store { get; }

    /// <summary>The chats, and every word the desk put in them.</summary>
    public IConversations Conversations { get; }

    public RecordingHandoffNotifier Notifier { get; }

    public FakePresenceStore Presence { get; }

    public TestTimeProvider Clock { get; }

    /// <summary>The widget whose chats the tests are about.</summary>
    public VisitorCaller Visitor { get; }

    /// <summary>Another widget, with a key of its own.</summary>
    public VisitorCaller Stranger { get; }

    public VisitorCaller Anonymous { get; }

    /// <summary>Starts the server.</summary>
    public static async Task<VisitorHandoffWorld> StartAsync()
    {
        TestTimeProvider clock = new(Start);
        FakeHandoffStore store = new(clock);
        IConversations conversations = new Conversations(new InMemoryConversationStore(clock), blobs: null);
        RecordingHandoffNotifier notifier = new();
        FakePresenceStore presence = new(clock, TimeSpan.FromSeconds(90));

        var host = await ThreadTestHost.StartAsync(
            new NeonAuthTestKit(),
            services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IConversations>(conversations);
                services.AddSingleton<IHandoffStore>(store);
                services.AddSingleton<IHandoffNotifier>(notifier);
                services.AddSingleton<IPresenceStore>(presence);
                services.AddSingleton<IHandoffMailer>(new RecordingHandoffMailer());
                services.AddScoped<HandoffDesk>();
            },
            app =>
            {
                app.UseNeonAuthOnApi();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapVisitorHandoffs());
            },
            options => options.OpenPathPrefixes = ["/v1/public"]);

        return new VisitorHandoffWorld(host, store, conversations, notifier, presence, clock);
    }

    /// <summary>Makes a chat one widget owns, with one finished turn in it.</summary>
    public async Task<string> MakeChatAsync(VisitorCaller owner, string said = "the belt keeps slipping")
    {
        var conversationId = Guid.NewGuid().ToString("N");

        await Conversations.CreateAsync(conversationId, TestContext.Current.CancellationToken);
        await Conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(owner.Key!, app: null), TestContext.Current.CancellationToken);
        await Conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, said), TestContext.Current.CancellationToken);
        await Conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.Assistant, "Let me check."), TestContext.Current.CancellationToken);

        return conversationId;
    }

    /// <summary>Every word in a chat, as the store holds it.</summary>
    public async Task<IReadOnlyList<ConversationMessage>> WordsAsync(string conversationId)
        => await Conversations.ReadAsync(conversationId, TestContext.Current.CancellationToken);

    /// <summary>Puts this many members of staff on a socket, each a different person.</summary>
    public async Task StaffOnlineAsync(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await Presence.ConnectAsync(
                $"socket-{index}",
                $"user:staff-{index}",
                $"Staff {index}",
                HandoffAdmission.StaffKind,
                TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(TestContext.Current.CancellationToken);
        _host.Dispose();
    }
}
