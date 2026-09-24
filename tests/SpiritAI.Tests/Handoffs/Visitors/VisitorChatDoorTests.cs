using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Contacts;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Visitors;
using SpiritAI.PublicChat;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Contacts;
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
    private const string PublicResponses = "/v1/public/responses";
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
        var conversationId = await world.MakeChatAsync(VisitorPrincipal.KeyOf(VisitorKey));

        var response = await world.PostAsync(VisitorKey, conversationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, world.TurnsRun);
        Assert.Equal([conversationId], world.Sessions.Reopened);
    }

    [Fact]
    public async Task AChatAPersonHasIsRefusedByName()
    {
        await using var world = await World.StartAsync();
        var conversationId = await world.MakeChatAsync(VisitorPrincipal.KeyOf(VisitorKey));
        await world.Handoffs.AskAsync(conversationId, HandoffAskedBy.Visitor, null, HandoffSummary.Empty, TestContext.Current.CancellationToken);

        var response = await world.PostAsync(VisitorKey, conversationId);

        // A stale tab must not wake the bot into a chat a person is talking in.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, world.TurnsRun);
        Assert.Equal("handoff_open", await TypeOf(response));
    }

    [Fact]
    public async Task SomebodyElsesChatIsRefused()
    {
        await using var world = await World.StartAsync();
        var conversationId = await world.MakeChatAsync(VisitorPrincipal.KeyOf("widget-two"));

        var response = await world.PostAsync(VisitorKey, conversationId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, world.TurnsRun);
        Assert.Empty(world.Sessions.Reopened);
    }

    /// <summary>
    /// Ownership now reads <c>contact_conversation</c>, not <c>ThreadEnvelope.Owner</c>, so a
    /// conversation whose row never got written must stay refused, and naming it must not be a way
    /// to plant that row: the fix for the claiming hole step 1's per-turn <c>EnsureAsync</c> would
    /// have opened once ownership moved off <c>Owner</c>.
    /// </summary>
    [Fact]
    public async Task AnOwnedChatWithNoContactRowIsRefusedAndNoRowIsMade()
    {
        await using var world = await World.StartAsync();
        var conversationId = await world.MakeChatWithNoContactRowAsync(VisitorPrincipal.KeyOf(VisitorKey));

        var response = await world.PostAsync(VisitorKey, conversationId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, world.TurnsRun);
        Assert.DoesNotContain(world.ContactConversations.Rows, row => row.ConversationId == conversationId);
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
        private readonly InMemoryConversationStore _conversations;
        private readonly FakeContactResolver _contacts;
        private readonly FakeContactConversationStore _contactConversations;
        private readonly HttpClient _client;

        private World(
            IHost host,
            InMemoryConversationStore conversations,
            FakeContactResolver contacts,
            FakeContactConversationStore contactConversations,
            FakeHandoffStore handoffs,
            FakeSessions sessions,
            List<string> turns)
        {
            _host = host;
            _conversations = conversations;
            _contacts = contacts;
            _contactConversations = contactConversations;
            _client = host.GetTestClient();
            Handoffs = handoffs;
            Sessions = sessions;
            Turns = turns;
        }

        public FakeHandoffStore Handoffs { get; }

        public FakeSessions Sessions { get; }

        public FakeContactResolver Contacts => _contacts;

        public FakeContactConversationStore ContactConversations => _contactConversations;

        /// <summary>The thread each turn that reached the stub behind the door named.</summary>
        private List<string> Turns { get; }

        /// <summary>How many turns reached the stub behind the door.</summary>
        public int TurnsRun => Turns.Count;

        public async Task<string> MakeChatAsync(string ownerKey)
        {
            var conversationId = Guid.NewGuid().ToString("N");

            await _conversations.CreateAsync(conversationId, TestContext.Current.CancellationToken);
            await _conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(ownerKey, app: null), TestContext.Current.CancellationToken);

            var contactId = await _contacts.ResolveAsync(ownerKey, TestContext.Current.CancellationToken);
            await _contactConversations.EnsureAsync(conversationId, contactId, ContactChannel.Chat, TestContext.Current.CancellationToken);

            return conversationId;
        }

        /// <summary>
        /// Makes a chat with <c>ThreadEnvelope.Owner</c> set but no <c>contact_conversation</c> row,
        /// the shape a conversation the backfill has not reached yet would have.
        /// </summary>
        public async Task<string> MakeChatWithNoContactRowAsync(string ownerKey)
        {
            var conversationId = Guid.NewGuid().ToString("N");

            await _conversations.CreateAsync(conversationId, TestContext.Current.CancellationToken);
            await _conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(ownerKey, app: null), TestContext.Current.CancellationToken);

            return conversationId;
        }

        public Task<HttpResponseMessage> PostAsync(string? visitor, string? thread)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, PublicResponses);

            if (visitor is not null)
            {
                request.Headers.TryAddWithoutValidation(VisitorPrincipal.Header, visitor);
            }

            // A Responses turn names its chat in the body.
            request.Content = JsonContent.Create(new
            {
                input = "hello",
                stream = false,
                conversation = thread,
            });

            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public static async Task<World> StartAsync()
        {
            TestTimeProvider clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
            InMemoryConversationStore conversations = new(clock);
            FakeContactResolver contacts = new();
            FakeContactConversationStore contactConversations = new();
            FakeHandoffStore handoffs = new(clock);
            FakeSessions sessions = new();
            List<string> turns = [];

            var host = await ThreadTestHost.StartAsync(
                new NeonAuthTestKit(),
                services =>
                {
                    services.AddSingleton<IConversations>(new Conversations(conversations, blobs: null));
                    services.AddSingleton<IHandoffStore>(handoffs);
                    services.AddSingleton<IThreadSessions>(sessions);
                    services.AddSingleton<IContactResolver>(contacts);
                    services.AddSingleton<IContactConversationStore>(contactConversations);
                },
                app =>
                {
                    app.UseNeonAuthOnApi();
                    app.UseVisitorChat(PublicResponses);
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost(PublicResponses, () =>
                        {
                            turns.Add("responses");
                            return Results.Ok("ran");
                        });
                    });
                },
                options => options.OpenPathPrefixes = [PublicResponses]);

            return new World(host, conversations, contacts, contactConversations, handoffs, sessions, turns);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }
    }
}
