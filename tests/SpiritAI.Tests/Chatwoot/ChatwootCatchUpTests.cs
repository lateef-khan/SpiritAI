using AgentCore.Application.Conversation;
using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// How the AI's copy catches up with Chatwoot before a turn (spec section 7). The pages are a live
/// Chatwoot's: "Visitor 1" to "Bot 40" while the AI had the chat, then "Staff 41", "Visitor 42",
/// and "Staff 43" while a person had it, then "Visitor 44", and "Visitor 45" (id 147), which is
/// the message of the turn.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ChatwootCatchUpTests(PostgresFixture fixture)
{
    private const int TurnMessageId = 147;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>What the AI reads for "Staff 41", "Visitor 42", and "Staff 43".</summary>
    private const string StaffPhase =
        """
        While you were away, a person from our support team had this chat with the customer. You did not write these lines.
        Matthew (support team): Staff 41
        Customer: Visitor 42
        Matthew (support team): Staff 43
        What the support team said or promised, the company said and promised. Do not deny it or change it.
        """;

    [Fact]
    public async Task FromABookmarkOnTheNewestPageTheStaffPartIsOneNoteAndTheVisitorAfterItStaysTheirs()
    {
        await using var world = await World.OpenAsync(fixture, ["catchup_page1"], through: 140);

        var nextOrdinal = await world.CatchUpAsync();

        Assert.Equal([("system", StaffPhase), ("user", "Visitor 44")], await world.WordsAsync());

        Assert.Equal(2, nextOrdinal);
        Assert.Equal(TurnMessageId, await world.BookmarkAsync());
        Assert.Single(world.Wire.Requests);
    }

    [Fact]
    public async Task WhenTheCopyIsUpToDateNothingIsCopied()
    {
        await using var world = await World.OpenAsync(fixture, ["catchup_page1"], through: 146);

        var nextOrdinal = await world.CatchUpAsync();

        Assert.Empty(await world.WordsAsync());
        Assert.Equal(0, nextOrdinal);
        Assert.Equal(TurnMessageId, await world.BookmarkAsync());
        Assert.Single(world.Wire.Requests);
    }

    [Fact]
    public async Task ABookmarkOnTheSecondPageIsReachedByPagingBack()
    {
        await using var world = await World.OpenAsync(fixture, ["catchup_page1", "catchup_page2"], through: 120);

        await world.CatchUpAsync();

        Assert.Equal(
            [
                .. Enumerable.Range(21, 20).Where(i => i % 2 == 1).Select(i => ("user", $"Visitor {i}")),
                ("system", StaffPhase), ("user", "Visitor 44"),
            ],
            await world.WordsAsync());

        Assert.Equal(2, world.Wire.Requests.Count);
        Assert.EndsWith("/messages?before=126", world.Wire.Requests[1].Url);
        Assert.Equal(TurnMessageId, await world.BookmarkAsync());
    }

    [Fact]
    public async Task WithNoBookmarkTwoPagesAreReadBackAndTheBotsAnswersGoInAsTheAIs()
    {
        await using var world = await World.OpenAsync(fixture, ["catchup_page1", "catchup_page2", "catchup_page3"], through: null);

        await world.CatchUpAsync();

        Assert.Equal(
            [
                .. Enumerable.Range(6, 35).Select(i => i % 2 == 0 ? ("assistant", $"Bot {i}") : ("user", $"Visitor {i}")),
                ("system", StaffPhase), ("user", "Visitor 44"),
            ],
            await world.WordsAsync());

        Assert.Equal(2, world.Wire.Requests.Count);
        Assert.Equal(TurnMessageId, await world.BookmarkAsync());
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly PostgresFixture _fixture;
        private readonly IConversationStore _store;
        private readonly IConversations _conversations;
        private readonly string _conversationId = "test-cw-" + Guid.NewGuid().ToString("N");

        private World(PostgresFixture fixture, IConversationStore store, ReplayingHandler wire)
        {
            _fixture = fixture;
            _store = store;
            _conversations = new Conversations(store, blobs: null);
            Wire = wire;
        }

        public ReplayingHandler Wire { get; }

        public static async Task<World> OpenAsync(PostgresFixture fixture, IReadOnlyList<string> pages, int? through)
        {
            var world = new World(fixture, await fixture.OpenConversationStoreAsync(), new ReplayingHandler(pages));

            await world._conversations.CreateAsync(world._conversationId, Cancel);

            if (through is { } id)
            {
                await world._conversations.SetCustomAsync(world._conversationId, ChatwootBookmark.Write(custom: null, id), Cancel);
            }

            return world;
        }

        public async Task<int> CatchUpAsync()
        {
            var chatwoot = new ChatwootClient(new HttpClient(Wire), Options.Create(new ChatwootOptions
            {
                BaseUrl = "http://chatwoot.test/",
                InboxIdentifier = "inbox-key",
            }));

            var newest = await chatwoot.ListMessagesAsync("probe-catchup-key", 19, before: null, Cancel);

            var copy = await _conversations.GetAsync(_conversationId, Cancel);

            return await new ChatwootCatchUp(chatwoot, _conversations)
                .CatchUpAsync(copy!, new ChatwootIds(19, "probe-catchup-key"), TurnMessageId, newest!, Cancel);
        }

        public Task<IReadOnlyList<ConversationMessage>> RowsAsync()
            => _store.ReadForSessionAsync(_conversationId, Cancel).AsTask();

        public async Task<IReadOnlyList<(string Role, string Text)>> WordsAsync()
            => [.. (await RowsAsync()).Select(r => (r.Content.Role.Value, r.Content.Text))];

        public async Task<int?> BookmarkAsync()
            => ChatwootBookmark.Read((await _conversations.GetAsync(_conversationId, Cancel))!.Custom);

        public async ValueTask DisposeAsync()
        {
            await _fixture.DeleteConversationAsync(_conversationId);

            if (_store is IAsyncDisposable pool)
            {
                await pool.DisposeAsync();
            }
        }
    }
}
