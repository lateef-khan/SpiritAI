using System.Net;
using System.Text.Json;

using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Visitors;
using SpiritAI.PublicChat;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Threads;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Visitors;

/// <summary>
/// The door in front of the public chat route, section 9.3 of the handoff spec: the bot stays
/// quiet while a person has the chat, and a reloaded widget carries on where it was.
/// </summary>
public sealed class VisitorChatDoorTests
{
    private const string PublicChat = "/v1/public/chat/completions";
    private const string SessionHeader = "X-AgentCore-Session";
    private const string VisitorKey = "widget-one";

    [Fact]
    public async Task ATurnWithNoKeyIsTodaysWidgetAndPasses()
    {
        await using var world = await World.StartAsync();

        var response = await world.PostAsync(visitor: null, thread: "anything at all");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, world.TurnsRun);
        Assert.Empty(world.Sessions.Reopened);
    }

    [Fact]
    public async Task AnOwnedChatNobodyHasIsReopenedAndPasses()
    {
        await using var world = await World.StartAsync();
        var callId = await world.MakeChatAsync(VisitorPrincipal.KeyOf(VisitorKey));

        var response = await world.PostAsync(VisitorKey, callId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, world.TurnsRun);
        Assert.Equal([callId], world.Sessions.Reopened);
    }

    [Fact]
    public async Task AChatAPersonHasIsRefusedByName()
    {
        await using var world = await World.StartAsync();
        var callId = await world.MakeChatAsync(VisitorPrincipal.KeyOf(VisitorKey));
        await world.Handoffs.AskAsync(callId, HandoffAskedBy.Visitor, null, TestContext.Current.CancellationToken);

        var response = await world.PostAsync(VisitorKey, callId);

        // A stale tab must not wake the bot into a chat a person is talking in.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, world.TurnsRun);
        Assert.Equal("handoff_open", await TypeOf(response));
    }

    [Fact]
    public async Task SomebodyElsesChatIsRefused()
    {
        await using var world = await World.StartAsync();
        var callId = await world.MakeChatAsync(VisitorPrincipal.KeyOf("widget-two"));

        var response = await world.PostAsync(VisitorKey, callId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, world.TurnsRun);
        Assert.Empty(world.Sessions.Reopened);
    }

    [Fact]
    public async Task AKeyThisHostWillNotFileUnderIsRefused()
    {
        await using var world = await World.StartAsync();

        var response = await world.PostAsync("a b", thread: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, world.TurnsRun);
    }

    /// <summary>Reads the <c>type</c> of a problem response.</summary>
    private static async Task<string?> TypeOf(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return problem.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly InMemoryCallStore _calls;
        private readonly HttpClient _client;

        private World(IHost host, InMemoryCallStore calls, FakeHandoffStore handoffs, FakeSessions sessions, List<string> turns)
        {
            _host = host;
            _calls = calls;
            _client = host.GetTestClient();
            Handoffs = handoffs;
            Sessions = sessions;
            Turns = turns;
        }

        public FakeHandoffStore Handoffs { get; }

        public FakeSessions Sessions { get; }

        /// <summary>The thread each turn that reached the stub behind the door named.</summary>
        private List<string> Turns { get; }

        /// <summary>How many turns reached the stub behind the door.</summary>
        public int TurnsRun => Turns.Count;

        public async Task<string> MakeChatAsync(string ownerKey)
        {
            var callId = Guid.NewGuid().ToString("N");

            await _calls.CreateAsync(callId, TestContext.Current.CancellationToken);
            await _calls.SetCustomAsync(callId, ThreadEnvelope.Build(ownerKey, app: null), TestContext.Current.CancellationToken);

            return callId;
        }

        public Task<HttpResponseMessage> PostAsync(string? visitor, string? thread)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, PublicChat);

            if (visitor is not null)
            {
                request.Headers.TryAddWithoutValidation(VisitorPrincipal.Header, visitor);
            }

            if (thread is not null)
            {
                request.Headers.TryAddWithoutValidation(SessionHeader, thread);
            }

            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public static async Task<World> StartAsync()
        {
            TestTimeProvider clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
            InMemoryCallStore calls = new(clock);
            FakeHandoffStore handoffs = new(clock);
            FakeSessions sessions = new();
            List<string> turns = [];

            var host = await ThreadTestHost.StartAsync(
                new NeonAuthTestKit(),
                services =>
                {
                    services.AddSingleton<ICallStore>(calls);
                    services.AddSingleton<IHandoffStore>(handoffs);
                    services.AddSingleton<IThreadSessions>(sessions);
                },
                app =>
                {
                    app.UseNeonAuthOnApi();
                    app.UseVisitorChat(PublicChat);
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapPost(PublicChat, (HttpContext http) =>
                    {
                        turns.Add(http.Request.Headers[SessionHeader].ToString());
                        return Results.Ok("ran");
                    }));
                },
                options => options.OpenPathPrefixes = [PublicChat]);

            return new World(host, calls, handoffs, sessions, turns);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }
    }
}
