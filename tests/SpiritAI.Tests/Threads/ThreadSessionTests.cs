using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Tests.Auth;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>
/// The door in front of the Responses endpoint, for a thread that was opened days ago.
/// </summary>
public sealed class ThreadSessionTests
{
    private const string Responses = "/v1/main/responses";
    private const string PublicResponses = "/v1/public/main/responses";

    [Fact]
    public async Task ATurnThatNamesNoThreadIsLetThrough()
    {
        await using var world = await World.StartAsync();

        var response = await world.PostAsync(world.OwnerToken, thread: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(world.Sessions.Reopened);
    }

    [Fact]
    public async Task AnOwnedThreadWithNoLiveSessionIsReopened()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.MakeThreadAsync(World.OwnerKey);

        var response = await world.PostAsync(world.OwnerToken, remoteId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([remoteId], world.Sessions.Reopened);
    }

    [Fact]
    public async Task AThreadThatIsAlreadyLiveIsLeftAlone()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.MakeThreadAsync(World.OwnerKey);
        world.Sessions.MarkLive(remoteId);

        var response = await world.PostAsync(world.OwnerToken, remoteId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(world.Sessions.Reopened);
    }

    [Fact]
    public async Task SomebodyElsesThreadIsRefused()
    {
        await using var world = await World.StartAsync();
        var remoteId = await world.MakeThreadAsync(World.OwnerKey);

        var response = await world.PostAsync(world.StrangerToken, remoteId);

        // Without this the endpoint would happily continue the owner's conversation for
        // anybody who names its id.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(world.Sessions.Reopened);
    }

    [Fact]
    public async Task AThreadNobodyHasIsRefused()
    {
        await using var world = await World.StartAsync();

        var response = await world.PostAsync(world.OwnerToken, "no-such-thread");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheWidgetsOwnRouteIsNotTouched()
    {
        await using var world = await World.StartAsync();

        // Nobody behind the public bubble is signed in, so there is no principal to check a claim
        // against. Guarding that route would close it to everyone.
        var response = await world.PostPublicAsync(conversation: "anything at all");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(world.Sessions.Reopened);
    }

    private sealed class World : IAsyncDisposable
    {
        public const string OwnerSubject = "user_owner";
        public const string OwnerKey = CallerPrincipal.UserPrefix + OwnerSubject;
        public const string StrangerSubject = "user_stranger";

        private readonly IHost _host;
        private readonly InMemoryCallStore _store;

        private World(IHost host, NeonAuthTestKit kit, InMemoryCallStore store, FakeSessions sessions)
        {
            _host = host;
            _store = store;
            Sessions = sessions;
            Client = host.GetTestClient();
            OwnerToken = kit.Token(subject: OwnerSubject);
            StrangerToken = kit.Token(subject: StrangerSubject);
        }

        public FakeSessions Sessions { get; }

        public HttpClient Client { get; }

        public string OwnerToken { get; }

        public string StrangerToken { get; }

        public async Task<string> MakeThreadAsync(string ownerKey)
        {
            var callId = Guid.NewGuid().ToString("N");

            await _store.CreateAsync(callId, TestContext.Current.CancellationToken);
            await _store.SetCustomAsync(
                callId, ThreadEnvelope.Build(ownerKey, app: null), TestContext.Current.CancellationToken);

            return callId;
        }

        public Task<HttpResponseMessage> PostAsync(string? token, string? thread)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Responses);

            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // A Responses turn names its thread in the body.
            request.Content = JsonContent.Create(new
            {
                input = "hello",
                stream = false,
                conversation = thread,
            });

            return Client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public Task<HttpResponseMessage> PostPublicAsync(string? conversation)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, PublicResponses);

            request.Content = JsonContent.Create(new
            {
                input = "hello",
                stream = false,
                conversation,
            });

            return Client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public static async Task<World> StartAsync()
        {
            var kit = new NeonAuthTestKit();
            InMemoryCallStore store = new();
            FakeSessions sessions = new();

            var host = await ThreadTestHost.StartAsync(
                kit,
                services =>
                {
                    services.AddSingleton<ICallStore>(store);
                    services.AddSingleton<IThreadSessions>(sessions);
                },
                app =>
                {
                    app.UseNeonAuthOnApi();
                    app.UseThreadSessions(Responses);
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost(Responses, () => Results.Ok("ran"));
                        endpoints.MapPost(PublicResponses, () => Results.Ok("ran"));
                    });
                },
                options => options.OpenPathPrefixes = [PublicResponses]);

            return new World(host, kit, store, sessions);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }
    }
}
