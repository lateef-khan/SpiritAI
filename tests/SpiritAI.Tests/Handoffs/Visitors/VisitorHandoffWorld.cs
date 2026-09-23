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
using SpiritAI.Chatwoot;
using SpiritAI.Contacts;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Mail;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Visitors;
using SpiritAI.PublicChat;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Contacts;
using SpiritAI.Tests.PublicChat;
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
        FakeContactResolver contacts,
        FakeContactConversationStore contactConversations,
        RecordingHandoffNotifier notifier,
        FakeStaffPresence staff,
        TestTimeProvider clock)
    {
        _host = host;
        Store = store;
        Conversations = conversations;
        Contacts = contacts;
        ContactConversations = contactConversations;
        Notifier = notifier;
        Staff = staff;
        Clock = clock;
        Visitor = new VisitorCaller(host.GetTestClient(), "widget-one");
        Stranger = new VisitorCaller(host.GetTestClient(), "widget-two");
        Anonymous = new VisitorCaller(host.GetTestClient(), visitorKey: null);
    }

    public FakeHandoffStore Store { get; }

    /// <summary>The chats, and every word the desk put in them.</summary>
    public IConversations Conversations { get; }

    public FakeContactResolver Contacts { get; }

    public FakeContactConversationStore ContactConversations { get; }

    public RecordingHandoffNotifier Notifier { get; }

    public FakeStaffPresence Staff { get; }

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
        FakeContactResolver contacts = new();
        FakeContactConversationStore contactConversations = new();
        RecordingHandoffNotifier notifier = new();
        FakeStaffPresence staff = new();

        var host = await ThreadTestHost.StartAsync(
            new NeonAuthTestKit(),
            services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IConversations>(conversations);
                services.AddSingleton<IContactResolver>(contacts);
                services.AddSingleton<IContactConversationStore>(contactConversations);
                services.AddSingleton<IHandoffStore>(store);
                services.AddSingleton<IHandoffNotifier>(notifier);
                services.AddSingleton<IStaffPresence>(staff);
                services.AddSingleton<IHandoffMailer>(new RecordingHandoffMailer());
                services.AddScoped<HandoffDesk>();
                services.AddOptions();
                services.AddSingleton<ChatwootCopyQueue>();
            },
            app =>
            {
                app.UseNeonAuthOnApi();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapVisitorHandoffs());
            },
            options => options.OpenPathPrefixes = ["/v1/public"]);

        return new VisitorHandoffWorld(host, store, conversations, contacts, contactConversations, notifier, staff, clock);
    }

    /// <summary>Makes a chat one widget owns, with one finished turn in it.</summary>
    public async Task<string> MakeChatAsync(VisitorCaller owner, string said = "the belt keeps slipping")
    {
        var conversationId = Guid.NewGuid().ToString("N");

        await Conversations.CreateAsync(conversationId, TestContext.Current.CancellationToken);
        await Conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(owner.Key!, app: null), TestContext.Current.CancellationToken);
        await Conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, said), TestContext.Current.CancellationToken);
        await Conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.Assistant, "Let me check."), TestContext.Current.CancellationToken);

        var contactId = await Contacts.ResolveAsync(owner.Key!, TestContext.Current.CancellationToken);
        await ContactConversations.EnsureAsync(conversationId, contactId, ContactChannel.Chat, TestContext.Current.CancellationToken);

        return conversationId;
    }

    /// <summary>Every word in a chat, as the store holds it.</summary>
    public async Task<IReadOnlyList<ConversationMessage>> WordsAsync(string conversationId)
        => await Conversations.AllAsync(conversationId, TestContext.Current.CancellationToken);

    /// <summary>Says this many members of staff are online.</summary>
    public void StaffOnline(int count) => Staff.Online = count;

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(TestContext.Current.CancellationToken);
        _host.Dispose();
    }
}
