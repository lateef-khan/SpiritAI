using System.Net;
using System.Net.Http.Json;

using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.PublicChat;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// The door in front of the public turn route (spec 6.2). Chatwoot's answers are a live
/// Chatwoot's: conversation 18 (<c>e86f9a8c-…</c>) is pending and holds "My treadmill belt slips."
/// (95), a bot answer (96), and a staff reply (98); conversation 20 (<c>450b5ac6-…</c>) is open.
/// </summary>
public sealed class ChatwootTurnMiddlewareTests
{
    private const string Route = "/v1/public/main/responses";
    private const string VisitorKey = "probe-visitor-key-1";
    private const string Pending = "cw_e86f9a8c-9b8c-4b84-9c3b-16b72acdbea1";
    private const string Open = "cw_450b5ac6-881d-4509-829a-22760ca0415b";
    private const string Answer = "Which model is it?";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ATurnWithoutTheChatwootHeadersIsRefused()
    {
        await using var world = await World.StartAsync(new ReplayingHandler("visitor_messages"));

        var response = await world.PostAsync(Pending, displayId: 18, messageId: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(world.Wire.Requests);
        Assert.Empty(world.Turns);
    }

    [Fact]
    public async Task AConversationChatwootDoesNotListForTheVisitorIsNotTheirs()
    {
        await using var world = await World.StartAsync(new ReplayingHandler(payload: null, HttpStatusCode.NotFound));

        var response = await world.PostAsync(Pending, displayId: 18, messageId: 95);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts/probe-visitor-key-1/conversations/18/messages",
            Assert.Single(world.Wire.Requests).Url);
        Assert.Empty(world.Turns);
    }

    [Fact]
    public async Task ABodyThatNamesAnotherConversationIsRefused()
    {
        await using var world = await World.StartAsync(new ReplayingHandler(["visitor_messages", "conversation_shown"]));

        var response = await world.PostAsync("cw_" + Guid.NewGuid().ToString("D"), displayId: 18, messageId: 95);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(world.Turns);
    }

    [Fact]
    public async Task WhileAPersonHasTheChatTheAIDoesNotAnswer()
    {
        await using var world = await World.StartAsync(new ReplayingHandler(["visitor_messages", "conversation_shown_open"]));

        var response = await world.PostAsync(Open, displayId: 20, messageId: 95);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("bot-token", world.Wire.Requests[1].Token);
        Assert.Empty(world.Turns);
        Assert.Null(await world.Conversations.GetAsync(Open, Cancel));
    }

    [Fact]
    public async Task AFirstTurnMakesTheCopyFilesItsIdsAndCatchesItUpBeforeTheTurnRuns()
    {
        await using var world = await World.StartAsync(new ReplayingHandler(["visitor_messages", "conversation_shown", null]));

        var response = await world.PostAsync(Pending, displayId: 18, messageId: 95);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var turn = Assert.Single(world.Turns);
        Assert.Equal(["Sorry to hear that. **Which model** is it?", "Hi, this is Matthew from support."], turn.CopyBeforeTheTurn);

        var copy = await world.Conversations.GetAsync(Pending, Cancel);
        Assert.Equal(98, ChatwootBookmark.Read(copy!.Custom));
        Assert.Equal(new ChatwootIds(18, VisitorKey), ChatwootIds.Read(copy.Custom));
    }

    [Fact]
    public async Task OnlyTheAnswerOfTheTurnIsPostedToChatwootAsTheBot()
    {
        await using var world = await World.StartAsync(new ReplayingHandler(["visitor_messages", "conversation_shown", null]));

        await world.PostAsync(Pending, displayId: 18, messageId: 95);

        var post = await world.WaitForRequestAsync(2);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/conversations/18/messages", post.Url);
        Assert.Equal($$"""{"content":"{{Answer}}","message_type":"outgoing"}""", post.Body);
        Assert.Equal("bot-token", post.Token);
        Assert.Equal(3, world.Wire.Requests.Count);
    }

    /// <summary>What the stub turn saw: the scoped ids, and the words the copy held before it ran.</summary>
    private sealed record SeenTurn(IReadOnlyList<string> CopyBeforeTheTurn);

    private sealed class World(IHost host, ReplayingHandler wire, IConversations conversations, List<SeenTurn> turns) : IAsyncDisposable
    {
        private readonly HttpClient _client = host.GetTestClient();

        public ReplayingHandler Wire { get; } = wire;

        public IConversations Conversations { get; } = conversations;

        public List<SeenTurn> Turns { get; } = turns;

        public static async Task<World> StartAsync(ReplayingHandler wire)
        {
            IConversations conversations = new Conversations(new InMemoryConversationStore(), blobs: null);
            List<SeenTurn> turns = [];
            var options = Options.Create(new ChatwootOptions
            {
                BaseUrl = "http://chatwoot.test/",
                AccountId = 2,
                InboxIdentifier = "inbox-key",
                BotToken = "bot-token",
            });

            var host = await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddSingleton(options);
                        services.AddSingleton(conversations);
                        services.AddSingleton(new ChatwootClient(new HttpClient(wire), options));
                        services.AddScoped<ChatwootCatchUp>();
                        services.AddScoped<ChatwootAnswer>();
                    })
                    .Configure(app =>
                    {
                        app.UseChatwootTurn(Route);
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapPost(Route, async (HttpContext http) =>
                        {
                            var conversationId = await TurnConversation.ReadAsync(http.Request);
                            var before = await conversations.AllAsync(conversationId, Cancel);

                            turns.Add(new SeenTurn([.. before.Select(m => m.Content.Text)]));

                            await conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, "My treadmill belt slips."), Cancel);
                            await conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.Assistant, Answer), Cancel);

                            return Results.Ok("ran");
                        }));
                    }))
                .StartAsync(Cancel);

            return new World(host, wire, conversations, turns);
        }

        public Task<HttpResponseMessage> PostAsync(string conversation, int? displayId, int? messageId)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Route)
            {
                Content = JsonContent.Create(new { input = "My treadmill belt slips.", stream = false, conversation }),
            };

            request.Headers.Add(VisitorPrincipal.Header, VisitorKey);

            if (displayId is { } display)
            {
                request.Headers.Add(ChatwootTurn.ConversationHeader, display.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (messageId is { } message)
            {
                request.Headers.Add(ChatwootTurn.MessageHeader, message.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return _client.SendAsync(request, Cancel);
        }

        /// <summary>Waits for a request that is sent after the response, as the answer post is.</summary>
        public async Task<(string Url, string Body, string? Token)> WaitForRequestAsync(int index)
        {
            for (var tries = 0; Wire.Requests.Count <= index; tries++)
            {
                Assert.True(tries < 100, $"Chatwoot got no request {index} in time.");
                await Task.Delay(50, Cancel);
            }

            return Wire.Requests[index];
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await host.StopAsync(Cancel);
            host.Dispose();
        }
    }
}
