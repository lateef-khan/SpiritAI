using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Reads;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Reads;

/// <summary>
/// The facts about a read mark only PostgreSQL can keep: it lands on the chat's latest line, it
/// is each reader's own, it never moves back, and a chat with nothing in it gets none.
/// </summary>
public sealed class ConversationReadStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AMarkLandsOnTheLatestLineForTheReaderAlone()
    {
        var conversationId = "test-" + Guid.NewGuid().ToString("N");
        var conversations = await fixture.OpenConversationStoreAsync();

        try
        {
            await conversations.CreateAsync(conversationId, Cancel);
            await conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, "hello?"), Cancel);
            var latest = await conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.Assistant, "Let me check."), Cancel);

            await using var database = fixture.Open();
            var store = new ConversationReadStore(database, new TestTimeProvider(Start));

            await store.MarkSeenAsync(conversationId, "staff:dana", Cancel);

            var danas = await store.SeenAsync("staff:dana", [conversationId], Cancel);
            var sams = await store.SeenAsync("staff:sam", [conversationId], Cancel);

            Assert.Equal(latest.Ordinal, danas[conversationId]);
            Assert.Empty(sams);
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
            await DisposeAsync(conversations);
        }
    }

    [Fact]
    public async Task AMarkFollowsNewLinesAndNeverMovesBack()
    {
        var conversationId = "test-" + Guid.NewGuid().ToString("N");
        var conversations = await fixture.OpenConversationStoreAsync();

        try
        {
            await conversations.CreateAsync(conversationId, Cancel);
            await conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, "hello?"), Cancel);

            await using var database = fixture.Open();
            var store = new ConversationReadStore(database, new TestTimeProvider(Start));

            await store.MarkSeenAsync(conversationId, "staff:dana", Cancel);
            var later = await conversations.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, "still there?"), Cancel);
            await store.MarkSeenAsync(conversationId, "staff:dana", Cancel);

            // A second mark on the same line is a no-op, not a step back.
            await store.MarkSeenAsync(conversationId, "staff:dana", Cancel);

            var seen = await store.SeenAsync("staff:dana", [conversationId], Cancel);

            Assert.Equal(later.Ordinal, seen[conversationId]);
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
            await DisposeAsync(conversations);
        }
    }

    [Fact]
    public async Task AnEmptyChatGetsNoMark()
    {
        var conversationId = "test-" + Guid.NewGuid().ToString("N");
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var store = new ConversationReadStore(database, new TestTimeProvider(Start));

            await store.MarkSeenAsync(conversationId, "staff:dana", Cancel);

            Assert.Empty(await store.SeenAsync("staff:dana", [conversationId], Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    private static async Task DisposeAsync(object store)
    {
        if (store is IAsyncDisposable pool)
        {
            await pool.DisposeAsync();
        }
    }
}
