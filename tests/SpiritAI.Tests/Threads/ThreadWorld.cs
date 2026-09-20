using AgentCore.Application.Blobs;
using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Blobs;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>The host, the store and the two callers one test needs.</summary>
internal sealed class ThreadWorld : IAsyncDisposable
{
    public const string Threads = "/v1/threads";

    public const string OwnerSubject = "user_owner";
    public const string StrangerSubject = "user_stranger";

    private readonly IHost _host;

    private ThreadWorld(IHost host, NeonAuthTestKit kit, Conversations store, InMemoryBlobStore blobs)
    {
        _host = host;
        Store = store;
        Words = store;
        Blobs = blobs;
        Owner = new ThreadCaller(host.GetTestClient(), kit.Token(subject: OwnerSubject));
        Stranger = new ThreadCaller(host.GetTestClient(), kit.Token(subject: StrangerSubject));
        Anonymous = new ThreadCaller(host.GetTestClient(), token: null);
    }

    public ThreadCaller Owner { get; }

    public ThreadCaller Stranger { get; }

    public ThreadCaller Anonymous { get; }

    /// <summary>The store behind the routes, so a test can put words in a thread.</summary>
    public IConversations Store { get; }

    /// <summary>The same store as the turn loop sees it, so a test can file words under a turn.</summary>
    public IConversationStore Words { get; }

    public InMemoryBlobStore Blobs { get; }

    public static async Task<ThreadWorld> StartAsync()
    {
        var kit = new NeonAuthTestKit();
        InMemoryBlobStore blobs = new();
        Conversations store = new(new InMemoryConversationStore(), blobs);

        var host = await ThreadTestHost.StartAsync(
            kit,
            services =>
            {
                services.AddSingleton<IConversations>(store);
                services.AddSingleton<IConversationTitler>(new SpellingTitler(store));
            },
            app =>
            {
                app.UseNeonAuthOnApi();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapThreads());
            });

        return new ThreadWorld(host, kit, store, blobs);
    }

    /// <summary>Writes one finished turn into store 1, the way a real turn would.</summary>
    public async ValueTask SayAsync(string remoteId, string said, string heard)
    {
        await Store.AppendMessageAsync(remoteId, new ChatMessage(ChatRole.User, said), TestContext.Current.CancellationToken);
        await Store.AppendMessageAsync(remoteId, new ChatMessage(ChatRole.Assistant, heard), TestContext.Current.CancellationToken);
    }

    /// <summary>Writes one finished turn into store 1 under the turn index the turn loop would give it.</summary>
    public async ValueTask TurnAsync(string remoteId, int turn, string said, string heard)
    {
        await Words.AppendAsync(
            remoteId,
            [
                new ConversationMessageDraft(turn, new ChatMessage(ChatRole.User, said), $"{remoteId}-{turn}-q"),
                new ConversationMessageDraft(turn, new ChatMessage(ChatRole.Assistant, heard), $"{remoteId}-{turn}-a"),
            ],
            cancellationToken: TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(TestContext.Current.CancellationToken);
        _host.Dispose();
    }
}
