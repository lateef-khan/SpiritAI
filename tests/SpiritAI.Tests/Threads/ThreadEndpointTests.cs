using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Tests.Auth;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>
/// The thread list, over the wire, with a real token on every request.
/// </summary>
public sealed class ThreadEndpointTests
{
    private const string Threads = "/v1/threads";

    [Fact]
    public async Task ANewCallerHasNoThreads()
    {
        await using var world = await World.StartAsync();

        var page = await world.Owner.ListAsync();

        Assert.Empty(page.Threads);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task CreatingAThreadAnswersWithItsRemoteId()
    {
        await using var world = await World.StartAsync();

        var response = await world.Owner.PostAsync(Threads);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ThreadCreated>();
        Assert.False(string.IsNullOrWhiteSpace(created.RemoteId));
    }

    [Fact]
    public async Task ACreatedThreadIsInItsOwnersList()
    {
        await using var world = await World.StartAsync();

        var remoteId = await world.Owner.CreateThreadAsync();
        var page = await world.Owner.ListAsync();

        Assert.Equal(remoteId, Assert.Single(page.Threads).RemoteId);
    }

    [Fact]
    public async Task TitlingAThreadNamesItFromTheWordsTheBrowserSent()
    {
        await using var world = await World.StartAsync();

        var remoteId = await world.Owner.CreateThreadAsync();

        var streamed = await world.Owner.TitleAsync(remoteId, Said("the belt keeps slipping badly"));

        // Nothing was ever written to store 1, so this name can only have come from the body.
        Assert.Equal("the belt keeps", streamed);
        Assert.Equal("the belt keeps", (await world.Owner.FetchAsync(remoteId)).Title);
    }

    [Fact]
    public async Task TitlingAThreadWithNoWordsLeavesItUnnamed()
    {
        await using var world = await World.StartAsync();

        var remoteId = await world.Owner.CreateThreadAsync();

        var streamed = await world.Owner.TitleAsync(remoteId, new { messages = Array.Empty<object>() });

        Assert.Equal(string.Empty, streamed);
        Assert.Null((await world.Owner.FetchAsync(remoteId)).Title);
    }

    [Fact]
    public async Task TitlingRefusesABodyThatIsNotAThreadsWorth()
    {
        await using var world = await World.StartAsync();

        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Owner.PostAsync($"{Threads}/{remoteId}/title", new { messages = "belt" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await world.Owner.FetchAsync(remoteId)).Title);
    }

    /// <summary>One caller turn, in the shape the browser posts it.</summary>
    private static object Said(string words) => new { messages = new[] { new { role = "user", content = words } } };

    [Fact]
    public async Task AStrangerCannotTitleSomebodyElsesThread()
    {
        await using var world = await World.StartAsync();

        var remoteId = await world.Owner.CreateThreadAsync();
        await world.SayAsync(remoteId, "the belt keeps slipping badly", "Try the tension bolt.");

        var response = await world.Stranger.PostAsync($"{Threads}/{remoteId}/title");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null((await world.Owner.FetchAsync(remoteId)).Title);
    }

    [Fact]
    public async Task TitlingNeedsAToken()
    {
        await using var world = await World.StartAsync();

        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Anonymous.PostAsync($"{Threads}/{remoteId}/title");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ANewThreadIsRegularAndUntitled()
    {
        await using var world = await World.StartAsync();

        var remoteId = await world.Owner.CreateThreadAsync();
        var thread = await world.Owner.FetchAsync(remoteId);

        Assert.Equal("regular", thread.Status);
        Assert.Null(thread.Title);
    }

    [Fact]
    public async Task OneCallersThreadIsNotInAnothersList()
    {
        await using var world = await World.StartAsync();

        await world.Owner.CreateThreadAsync();
        var page = await world.Stranger.ListAsync();

        Assert.Empty(page.Threads);
    }

    [Fact]
    public async Task RenamingAThreadShowsInTheList()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Owner.PatchAsync($"{Threads}/{remoteId}", new { title = "Treadmill belt slips" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Treadmill belt slips", (await world.Owner.FetchAsync(remoteId)).Title);
    }

    [Fact]
    public async Task ArchivingMovesAThreadOutOfTheRegularListing()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        await world.Owner.PatchAsync($"{Threads}/{remoteId}", new { status = "archived" });

        Assert.Empty((await world.Owner.ListAsync("?status=regular")).Threads);
        Assert.Single((await world.Owner.ListAsync("?status=archived")).Threads);
    }

    [Fact]
    public async Task UnarchivingBringsAThreadBack()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();
        await world.Owner.PatchAsync($"{Threads}/{remoteId}", new { status = "archived" });

        await world.Owner.PatchAsync($"{Threads}/{remoteId}", new { status = "regular" });

        Assert.Single((await world.Owner.ListAsync("?status=regular")).Threads);
    }

    [Fact]
    public async Task AnUnknownStatusIsRefused()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Owner.PatchAsync($"{Threads}/{remoteId}", new { status = "burned" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CustomFieldsSurviveARoundTrip()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        await world.Owner.PatchAsync($"{Threads}/{remoteId}", new { custom = new { pinned = true } });

        var custom = (await world.Owner.FetchAsync(remoteId)).Custom;
        Assert.True(custom.HasValue);
        Assert.True(custom!.Value.GetProperty("pinned").GetBoolean());
    }

    [Fact]
    public async Task TheOwnersKeyNeverReachesTheBrowser()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        // The key is kept beside the row so ownership is one read rather than a scan of the whole
        // listing. That is an implementation detail of the host, and a browser that could read it
        // could also read whose it is.
        var body = await world.Owner.RawAsync($"{Threads}/{remoteId}");

        Assert.DoesNotContain(World.OwnerSubject, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WritingCustomFieldsDoesNotCostTheOwnerTheirThread()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        await world.Owner.PatchAsync($"{Threads}/{remoteId}", new { custom = new { pinned = true } });

        Assert.Equal(HttpStatusCode.NotFound, (await world.Stranger.GetAsync($"{Threads}/{remoteId}")).StatusCode);
        Assert.Single((await world.Owner.ListAsync()).Threads);
    }

    [Fact]
    public async Task DeletingAThreadRemovesIt()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Owner.DeleteAsync($"{Threads}/{remoteId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Owner.GetAsync($"{Threads}/{remoteId}")).StatusCode);
    }

    [Fact]
    public async Task AStrangerCannotReadAThread()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Stranger.GetAsync($"{Threads}/{remoteId}");

        // 404 and not 403, deliberately. A 403 would confirm the call id names something real,
        // which is the one thing a caller guessing ids wants to learn.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AStrangerCannotRenameAThread()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Stranger.PatchAsync($"{Threads}/{remoteId}", new { title = "mine now" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null((await world.Owner.FetchAsync(remoteId)).Title);
    }

    [Fact]
    public async Task AStrangerCannotDeleteAThread()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Stranger.DeleteAsync($"{Threads}/{remoteId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single((await world.Owner.ListAsync()).Threads);
    }

    [Fact]
    public async Task AnUnknownThreadIsNotFound()
    {
        await using var world = await World.StartAsync();

        var response = await world.Owner.GetAsync($"{Threads}/nothing-by-that-name");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AListingPagesWithItsCursor()
    {
        await using var world = await World.StartAsync();
        for (var i = 0; i < 3; i++)
        {
            await world.Owner.CreateThreadAsync();
        }

        var first = await world.Owner.ListAsync("?limit=2");
        var second = await world.Owner.ListAsync($"?limit=2&after={Uri.EscapeDataString(first.NextCursor!)}");

        Assert.Equal(2, first.Threads.Count);
        Assert.Single(second.Threads);
        Assert.Null(second.NextCursor);
        Assert.Empty(first.Threads.Select(t => t.RemoteId).Intersect(second.Threads.Select(t => t.RemoteId)));
    }

    [Fact]
    public async Task AnOversizedLimitIsCappedRatherThanRefused()
    {
        await using var world = await World.StartAsync();
        await world.Owner.CreateThreadAsync();

        var response = await world.Owner.GetAsync($"{Threads}?limit=100000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WithoutATokenTheListIsShut()
    {
        await using var world = await World.StartAsync();

        var response = await world.Anonymous.GetAsync(Threads);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ANewThreadHasNoMessages()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var history = await world.Owner.HistoryAsync(remoteId);

        Assert.Empty(history.Messages);
        Assert.Null(history.HeadId);
    }

    [Fact]
    public async Task AThreadsWordsComeBackChained()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();
        await world.SayAsync(remoteId, "hello", "hi there");

        var history = await world.Owner.HistoryAsync(remoteId);

        Assert.Equal(2, history.Messages.Count);
        Assert.Equal(history.Messages[^1].Message.Id, history.HeadId);
    }

    [Fact]
    public async Task AStrangerCannotReadAThreadsWords()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();
        await world.SayAsync(remoteId, "hello", "hi there");

        var response = await world.Stranger.GetAsync($"{Threads}/{remoteId}/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>The host, the store and the two callers one test needs.</summary>
    private sealed class World : IAsyncDisposable
    {
        public const string OwnerSubject = "user_owner";
        public const string StrangerSubject = "user_stranger";

        private readonly IHost _host;

        private World(IHost host, NeonAuthTestKit kit, InMemoryCallStore store)
        {
            _host = host;
            Store = store;
            Owner = new Caller(host.GetTestClient(), kit.Token(subject: OwnerSubject));
            Stranger = new Caller(host.GetTestClient(), kit.Token(subject: StrangerSubject));
            Anonymous = new Caller(host.GetTestClient(), token: null);
        }

        public Caller Owner { get; }

        public Caller Stranger { get; }

        public Caller Anonymous { get; }

        /// <summary>The store behind the routes, so a test can put words in a thread.</summary>
        public InMemoryCallStore Store { get; }

        public static async Task<World> StartAsync()
        {
            var kit = new NeonAuthTestKit();
            InMemoryCallStore store = new();

            var host = await ThreadTestHost.StartAsync(
                kit,
                services =>
                {
                    services.AddSingleton<ICallStore>(store);
                    services.AddSingleton<ICallTitler>(new SpellingTitler(store));
                },
                app =>
                {
                    app.UseNeonAuthOnApi();
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapThreads());
                });

            return new World(host, kit, store);
        }

        /// <summary>Writes one finished turn into store 1, the way a real turn would.</summary>
        public ValueTask SayAsync(string remoteId, string said, string heard)
            => Store.AppendAsync([
                new CallMessage(remoteId, 0, 0, new ChatMessage(ChatRole.User, said), "m0"),
                new CallMessage(remoteId, 1, 0, new ChatMessage(ChatRole.Assistant, heard), "m1"),
            ]);

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }
    }

    /// <summary>
    /// A titler with no model behind it, standing in for <see cref="ICallTitler"/>.
    /// </summary>
    private sealed class SpellingTitler(ICallStore calls) : ICallTitler
    {
        public async IAsyncEnumerable<string> GenerateAsync(
            string callId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var rows = await calls.ReadAsync(callId, cancellationToken).ConfigureAwait(false);

            if (rows.Count == 0)
            {
                yield break;
            }

            var words = rows[0].Content.Text.Split(' ').Take(3).ToArray();

            for (var at = 0; at < words.Length; at++)
            {
                yield return at == 0 ? words[at] : " " + words[at];
            }

            await calls.RenameAsync(callId, string.Join(' ', words), cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<string> GenerateFromAsync(
            string callId,
            IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var words = string.Join(' ', messages.Select(message => message.Text));

            if (string.IsNullOrWhiteSpace(words))
            {
                yield break;
            }

            var picked = words.Split(' ').Take(3).ToArray();

            for (var at = 0; at < picked.Length; at++)
            {
                yield return at == 0 ? picked[at] : " " + picked[at];
            }

            await calls.RenameAsync(callId, string.Join(' ', picked), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>One signed-in browser, or one that is not signed in at all.</summary>
    private sealed class Caller(HttpClient client, string? token)
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url, body: null);

        public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

        public Task<HttpResponseMessage> PatchAsync(string url, object body) => SendAsync(HttpMethod.Patch, url, body);

        public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, body: null);

        public async Task<string> CreateThreadAsync()
        {
            var response = await PostAsync(Threads);
            response.EnsureSuccessStatusCode();
            return (await response.ReadAsync<ThreadCreated>()).RemoteId;
        }

        public async Task<ThreadPage> ListAsync(string query = "")
        {
            var response = await GetAsync(Threads + query);
            response.EnsureSuccessStatusCode();
            return await response.ReadAsync<ThreadPage>();
        }

        public async Task<ThreadSummary> FetchAsync(string remoteId)
        {
            var response = await GetAsync($"{Threads}/{remoteId}");
            response.EnsureSuccessStatusCode();
            return await response.ReadAsync<ThreadSummary>();
        }

        public async Task<ThreadHistory> HistoryAsync(string remoteId)
        {
            var response = await GetAsync($"{Threads}/{remoteId}/messages");
            response.EnsureSuccessStatusCode();
            return await response.ReadAsync<ThreadHistory>();
        }

        public async Task<string> TitleAsync(string remoteId, object body)
        {
            var response = await PostAsync($"{Threads}/{remoteId}/title", body);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }

        public async Task<string> RawAsync(string url)
        {
            var response = await GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }

        private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body)
        {
            var request = new HttpRequestMessage(method, url);

            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            return client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}

internal static class ResponseExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonSerializer.Deserialize<T>(body, Json)
            ?? throw new InvalidOperationException($"The response body was JSON null: {body}");
    }
}
