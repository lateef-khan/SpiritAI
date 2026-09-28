using AgentCore.Application.Conversation;
using AgentCore.Application.Ports;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.PublicChat;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.PublicChat;

/// <summary>
/// The sweep of widget transcripts (spec 6.5): a copy with no principal goes once it has not
/// changed for <see cref="PublicChatOptions.TranscriptDays"/>. The rows are aged by years, so they
/// are the oldest in the shared database and fall in the first batch.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WidgetTranscriptSweeperTests(PostgresFixture fixture)
{
    private static readonly TimeSpan PastTheLimit = TimeSpan.FromDays(3650);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnOldCopyWithNoPrincipalGoesAndTheOthersStay()
    {
        var store = await fixture.OpenConversationStoreAsync();
        IConversations conversations = new Conversations(store, blobs: null);

        var oldWidget = "test-sweep-" + Guid.NewGuid().ToString("N");
        var newWidget = "test-sweep-" + Guid.NewGuid().ToString("N");
        var oldThread = "test-sweep-" + Guid.NewGuid().ToString("N");

        try
        {
            await conversations.CreateAsync(oldWidget, Cancel);
            await conversations.CreateAsync(newWidget, Cancel);
            await conversations.CreateAsync(oldThread, Cancel);
            await conversations.AttachPrincipalAsync(oldThread, "user:someone", "owner", Cancel);

            await fixture.AgeConversationAsync(oldWidget, PastTheLimit);
            await fixture.AgeConversationAsync(oldThread, PastTheLimit);

            var swept = await Sweeper(conversations).SweepOnceAsync(Cancel);

            Assert.True(swept >= 1);
            Assert.Null(await conversations.GetAsync(oldWidget, Cancel));
            Assert.NotNull(await conversations.GetAsync(newWidget, Cancel));
            Assert.NotNull(await conversations.GetAsync(oldThread, Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(oldWidget);
            await fixture.DeleteConversationAsync(newWidget);
            await fixture.DeleteConversationAsync(oldThread);

            if (store is IAsyncDisposable pool)
            {
                await pool.DisposeAsync();
            }
        }
    }

    private WidgetTranscriptSweeper Sweeper(IConversations conversations)
    {
        var services = new ServiceCollection()
            .AddScoped(_ => fixture.Open())
            .AddSingleton(conversations)
            .BuildServiceProvider();

        return new WidgetTranscriptSweeper(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PublicChatOptions()),
            TimeProvider.System,
            NullLogger<WidgetTranscriptSweeper>.Instance);
    }
}
