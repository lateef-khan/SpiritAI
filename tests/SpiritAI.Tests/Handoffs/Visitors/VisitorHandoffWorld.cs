using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;
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
/// real desk, fakes for every port under it, and the three widgets a test needs.
/// </summary>
internal sealed class VisitorHandoffWorld : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly IHost _host;

    private VisitorHandoffWorld(
        IHost host,
        FakeHandoffStore store,
        InMemoryCallStore calls,
        RecordingHandoffTranscript transcript,
        RecordingHandoffNotifier notifier,
        FakePresenceStore presence,
        TestTimeProvider clock)
    {
        _host = host;
        Store = store;
        Calls = calls;
        Transcript = transcript;
        Notifier = notifier;
        Presence = presence;
        Clock = clock;
        Visitor = new VisitorCaller(host.GetTestClient(), "widget-one");
        Stranger = new VisitorCaller(host.GetTestClient(), "widget-two");
        Anonymous = new VisitorCaller(host.GetTestClient(), visitorKey: null);
    }

    public FakeHandoffStore Store { get; }

    public InMemoryCallStore Calls { get; }

    /// <summary>What the desk appended. Empty when the world was started with another transcript.</summary>
    public RecordingHandoffTranscript Transcript { get; }

    public RecordingHandoffNotifier Notifier { get; }

    public FakePresenceStore Presence { get; }

    public TestTimeProvider Clock { get; }

    /// <summary>The widget whose chats the tests are about.</summary>
    public VisitorCaller Visitor { get; }

    /// <summary>Another widget, with a key of its own.</summary>
    public VisitorCaller Stranger { get; }

    public VisitorCaller Anonymous { get; }

    /// <summary>Starts the server.</summary>
    /// <param name="transcript">What the desk appends to, when a test wants something other than the recorder.</param>
    public static async Task<VisitorHandoffWorld> StartAsync(IHandoffTranscript? transcript = null)
    {
        TestTimeProvider clock = new(Start);
        FakeHandoffStore store = new(clock);
        InMemoryCallStore calls = new(clock);
        RecordingHandoffTranscript recording = new();
        RecordingHandoffNotifier notifier = new();
        FakePresenceStore presence = new(clock, TimeSpan.FromSeconds(90));

        var host = await ThreadTestHost.StartAsync(
            new NeonAuthTestKit(),
            services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<ICallStore>(calls);
                services.AddSingleton<IHandoffStore>(store);
                services.AddSingleton<IHandoffTranscript>(transcript ?? recording);
                services.AddSingleton<IHandoffNotifier>(notifier);
                services.AddSingleton<IPresenceStore>(presence);
                services.AddScoped<HandoffDesk>();
            },
            app =>
            {
                app.UseNeonAuthOnApi();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapVisitorHandoffs());
            },
            options => options.OpenPathPrefixes = ["/v1/public"]);

        return new VisitorHandoffWorld(host, store, calls, recording, notifier, presence, clock);
    }

    /// <summary>Makes a chat one widget owns, with one finished turn in it.</summary>
    public async Task<string> MakeChatAsync(VisitorCaller owner, string said = "the belt keeps slipping")
    {
        var callId = Guid.NewGuid().ToString("N");

        await Calls.CreateAsync(callId, TestContext.Current.CancellationToken);
        await Calls.SetCustomAsync(callId, ThreadEnvelope.Build(owner.Key!, app: null), TestContext.Current.CancellationToken);
        await Calls.AppendAsync([
            new CallMessage(callId, 0, 0, new ChatMessage(ChatRole.User, said), "m0"),
            new CallMessage(callId, 1, 0, new ChatMessage(ChatRole.Assistant, "Let me check."), "m1"),
        ]);

        return callId;
    }

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
