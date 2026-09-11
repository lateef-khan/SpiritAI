using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Auth.Users;
using SpiritAI.Handoffs;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Mail;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.Staff;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.RealTime.Presence;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Auth.Users;
using SpiritAI.Tests.RealTime;
using SpiritAI.Tests.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Staff;

/// <summary>
/// The inbox routes on a test server: a real token check in front, the real desk, fakes for every
/// port behind, and the four callers a test needs.
/// </summary>
internal sealed class StaffHandoffWorld : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly IHost _host;

    private StaffHandoffWorld(
        IHost host,
        NeonAuthTestKit kit,
        FakeHandoffStore store,
        InMemoryCallStore calls,
        RecordingHandoffTranscript transcript,
        RecordingHandoffNotifier notifier,
        TestTimeProvider clock)
    {
        _host = host;
        Store = store;
        Calls = calls;
        Transcript = transcript;
        Notifier = notifier;
        Clock = clock;
        Staff = new StaffCaller(host.GetTestClient(), kit.Token(subject: "user_dana", email: "dana@example.com"));
        OtherStaff = new StaffCaller(host.GetTestClient(), kit.Token(subject: "user_sam", email: "Sam@Example.com"));
        Dealer = new StaffCaller(host.GetTestClient(), kit.Token(subject: "user_dealer", email: "dealer@example.com"));
        Anonymous = new StaffCaller(host.GetTestClient(), token: null);
    }

    public FakeHandoffStore Store { get; }

    public InMemoryCallStore Calls { get; }

    /// <summary>What the routes appended. Empty when the world was started with another transcript.</summary>
    public RecordingHandoffTranscript Transcript { get; }

    public RecordingHandoffNotifier Notifier { get; }

    public TestTimeProvider Clock { get; }

    /// <summary>Dana R., a person in the directory.</summary>
    public StaffCaller Staff { get; }

    /// <summary>Sam, second on the list, signed in with the address in another case.</summary>
    public StaffCaller OtherStaff { get; }

    /// <summary>Signed in, and not on the list.</summary>
    public StaffCaller Dealer { get; }

    public StaffCaller Anonymous { get; }

    /// <summary>Starts the server.</summary>
    /// <param name="transcript">What the routes append to, when a test wants something other than the recorder.</param>
    public static async Task<StaffHandoffWorld> StartAsync(IHandoffTranscript? transcript = null)
    {
        var kit = new NeonAuthTestKit();
        TestTimeProvider clock = new(Start);
        FakeHandoffStore store = new(clock);
        InMemoryCallStore calls = new(clock);
        RecordingHandoffTranscript recording = new();
        RecordingHandoffNotifier notifier = new();

        var host = await ThreadTestHost.StartAsync(
            kit,
            services =>
            {
                services.AddSingleton<IUserDirectory>(new FakeUserDirectory(
                    new AuthUser("user_dana", "Dana Rivera", "dana@example.com"),
                    new AuthUser("user_sam", "Sam", "sam@example.com")));
                services.AddScoped<StaffGate>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<ICallStore>(calls);
                services.AddSingleton<IHandoffStore>(store);
                services.AddSingleton<IHandoffTranscript>(transcript ?? recording);
                services.AddSingleton<IHandoffNotifier>(notifier);
                services.AddSingleton<IPresenceStore>(new FakePresenceStore(clock, TimeSpan.FromSeconds(90)));
                services.AddSingleton<IHandoffMailer>(new RecordingHandoffMailer());
                services.AddScoped<HandoffDesk>();
            },
            app =>
            {
                app.UseNeonAuthOnApi();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapStaffHandoffs());
            });

        return new StaffHandoffWorld(host, kit, store, calls, recording, notifier, clock);
    }

    /// <summary>Makes a titled chat with one finished turn in it, the way a real one would have.</summary>
    public async Task<string> MakeChatAsync(string title, string said)
    {
        var callId = Guid.NewGuid().ToString("N");

        await Calls.CreateAsync(callId, TestContext.Current.CancellationToken);
        await Calls.RenameAsync(callId, title, TestContext.Current.CancellationToken);
        await Calls.AppendAsync([
            new CallMessage(callId, 0, 0, new ChatMessage(ChatRole.User, said), "m0"),
            new CallMessage(callId, 1, 0, new ChatMessage(ChatRole.Assistant, "Let me check."), "m1"),
        ]);

        return callId;
    }

    /// <summary>Asks for a person on a chat, a minute after the last ask, so the line has an order.</summary>
    public async Task AskAsync(string callId)
    {
        Clock.Now += TimeSpan.FromMinutes(1);
        await Store.AskAsync(callId, HandoffAskedBy.Visitor, null, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(TestContext.Current.CancellationToken);
        _host.Dispose();
    }
}
