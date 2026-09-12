using System.Net;

using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.PublicChat;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Threads;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.PublicChat;

/// <summary>
/// The widget's own thread over the wire: section 4.4 of the handoff spec, with the visitor's key
/// as the whole identity.
/// </summary>
public sealed class PublicThreadEndpointTests
{
    private const string Threads = "/v1/public/threads";

    [Fact]
    public async Task CreatingAThreadFilesItUnderTheVisitorsKey()
    {
        await using var world = await World.StartAsync();

        var response = await world.Visitor.PostAsync(Threads);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ThreadCreated>();
        Assert.False(string.IsNullOrWhiteSpace(created.RemoteId));

        var record = await world.Calls.GetAsync(created.RemoteId, TestContext.Current.CancellationToken);
        Assert.Equal(world.Visitor.Key, ThreadEnvelope.OwnerOf(record?.Custom));

        var page = await world.Calls.ListAsync(world.Visitor.Key!, after: null, limit: 10, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal([created.RemoteId], page.Calls.Select(call => call.CallId));
    }

    [Fact]
    public async Task WithoutAKeyNoThreadIsMade()
    {
        await using var world = await World.StartAsync();

        var response = await world.Anonymous.PostAsync(Threads);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("k/ey")]
    public async Task AKeyThisHostWillNotFileUnderIsRefused(string sent)
    {
        await using var world = await World.StartAsync();

        var response = await world.Caller(sent).PostAsync(Threads);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AKeyLongerThanTheLimitIsRefused()
    {
        await using var world = await World.StartAsync();

        var response = await world.Caller(new string('k', 200)).PostAsync(Threads);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheHistoryIsTheVisitorsToRead()
    {
        await using var world = await World.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor.Key!, "the belt keeps slipping");

        var history = await world.Visitor.ReadAsync<ThreadHistory>($"{Threads}/{callId}/messages");

        Assert.Equal(2, history.Messages.Count);
        Assert.Equal("the belt keeps slipping", Assert.IsType<ThreadTextPart>(history.Messages[0].Message.Content[0]).Text);
    }

    [Fact]
    public async Task AnotherVisitorsHistoryLooksLikeNoChatAtAll()
    {
        await using var world = await World.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor.Key!, "the belt keeps slipping");

        var response = await world.Stranger.GetAsync($"{Threads}/{callId}/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WithoutAKeyNoHistoryIsRead()
    {
        await using var world = await World.StartAsync();
        var callId = await world.MakeChatAsync(world.Visitor.Key!, "the belt keeps slipping");

        var response = await world.Anonymous.GetAsync($"{Threads}/{callId}/messages");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>The public thread routes on a test server, with the real token check leaving them open.</summary>
    private sealed class World : IAsyncDisposable
    {
        private readonly IHost _host;

        private World(IHost host, ICallStore calls)
        {
            _host = host;
            Calls = calls;
            Visitor = Caller("widget-one");
            Stranger = Caller("widget-two");
            Anonymous = Caller(null);
        }

        public ICallStore Calls { get; }

        public VisitorCaller Visitor { get; }

        public VisitorCaller Stranger { get; }

        public VisitorCaller Anonymous { get; }

        public VisitorCaller Caller(string? visitorKey) => new(_host.GetTestClient(), visitorKey);

        public static async Task<World> StartAsync()
        {
            ICallStore calls = new InMemoryCallStore();

            var host = await ThreadTestHost.StartAsync(
                new NeonAuthTestKit(),
                services => services.AddSingleton<ICallStore>(calls),
                app =>
                {
                    app.UseNeonAuthOnApi();
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapPublicThreads());
                },
                options => options.OpenPathPrefixes = ["/v1/public"]);

            return new World(host, calls);
        }

        /// <summary>Makes a chat owned by one key, with one finished turn in it.</summary>
        public async Task<string> MakeChatAsync(string ownerKey, string said)
        {
            var callId = Guid.NewGuid().ToString("N");

            await Calls.CreateAsync(callId, TestContext.Current.CancellationToken);
            await Calls.SetCustomAsync(callId, ThreadEnvelope.Build(ownerKey, app: null), TestContext.Current.CancellationToken);
            await Calls.AppendMessageAsync(callId, new ChatMessage(ChatRole.User, said), TestContext.Current.CancellationToken);
            await Calls.AppendMessageAsync(callId, new ChatMessage(ChatRole.Assistant, "Let me check."), TestContext.Current.CancellationToken);

            return callId;
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }
    }
}
