using Microsoft.EntityFrameworkCore;

using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Store;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Store;

/// <summary>
/// The flow of section 5 of the handoff spec, move by move, against real PostgreSQL.
/// </summary>
/// <remarks>
/// The clock starts in the year 2000. Position counts every waiting row in the table, and other
/// tests make theirs at the real time, so rows asked for here always stand ahead of them.
/// </remarks>
public sealed class HandoffStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AskMakesAWaitingRowAtTheBackOfTheLine()
    {
        var (first, second) = (NewConversationId(), NewConversationId());
        await fixture.MakeConversationAsync(first);
        await fixture.MakeConversationAsync(second);

        try
        {
            await using var database = fixture.Open();
            var clock = new TestTimeProvider(Start);
            var store = new HandoffStore(database, clock);

            var a = await store.AskAsync(first, HandoffAskedBy.Visitor, null, Cancel);
            clock.Now += TimeSpan.FromMinutes(1);
            var b = await store.AskAsync(second, HandoffAskedBy.Bot, "warranty claim", Cancel);

            Assert.Equal(1, a.Position);
            Assert.Equal(2, b.Position);
            Assert.Equal(HandoffStatus.Waiting, a.Row.Status);
            Assert.Equal(HandoffStatus.Waiting, b.Row.Status);
            Assert.Equal(HandoffAskedBy.Visitor, a.Row.AskedBy);
            Assert.Null(a.Row.Reason);
            Assert.Equal(HandoffAskedBy.Bot, b.Row.AskedBy);
            Assert.Equal("warranty claim", b.Row.Reason);
            Assert.Equal(Start, a.Row.AskedAt);
            Assert.Equal(Start.AddMinutes(1), b.Row.AskedAt);

            Assert.Equal(1, await store.PositionAsync(first, Cancel));
            Assert.Equal(2, await store.PositionAsync(second, Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(first);
            await fixture.DeleteConversationAsync(second);
        }
    }

    [Fact]
    public async Task AskingTwiceOnOneChatReturnsTheSameRow()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var store = new HandoffStore(database, new TestTimeProvider(Start));

            var once = await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel);
            var twice = await store.AskAsync(conversationId, HandoffAskedBy.Bot, "again", Cancel);

            Assert.Equal(once.Row.Id, twice.Row.Id);
            Assert.Equal(HandoffAskedBy.Visitor, twice.Row.AskedBy);
            Assert.Equal(1, await database.Handoffs.CountAsync(h => h.ConversationId == conversationId, Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task ClaimIsWonOnceThenAlreadyTaken()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var clock = new TestTimeProvider(Start);
            var store = new HandoffStore(database, clock);

            await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel);
            clock.Now += TimeSpan.FromMinutes(1);

            var dana = await store.ClaimAsync(conversationId, "staff:dana", "Dana R.", Cancel);

            Assert.Equal(HandoffClaimResult.Won, dana.Result);
            Assert.NotNull(dana.Row);
            Assert.Equal(HandoffStatus.Human, dana.Row.Status);
            Assert.Equal("staff:dana", dana.Row.AssigneeKey);
            Assert.Equal("Dana R.", dana.Row.AssigneeName);
            Assert.Equal(clock.Now, dana.Row.ClaimedAt);

            var sam = await store.ClaimAsync(conversationId, "staff:sam", "Sam", Cancel);

            Assert.Equal(HandoffClaimResult.AlreadyTaken, sam.Result);
            Assert.NotNull(sam.Row);
            Assert.Equal("staff:dana", sam.Row.AssigneeKey);
            Assert.Equal("Dana R.", sam.Row.AssigneeName);

            var nobody = await store.ClaimAsync(NewConversationId(), "staff:sam", "Sam", Cancel);

            Assert.Equal(HandoffClaimResult.NotWaiting, nobody.Result);
            Assert.Null(nobody.Row);
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task DoneClosesAndAskAgainOpensANewRow()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var clock = new TestTimeProvider(Start);
            var store = new HandoffStore(database, clock);

            var first = await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel);
            clock.Now += TimeSpan.FromMinutes(1);

            Assert.True(await store.DoneAsync(conversationId, Cancel));
            Assert.Null(await store.OpenAsync(conversationId, Cancel));

            var closed = await store.LatestAsync(conversationId, Cancel);

            Assert.NotNull(closed);
            Assert.Equal(first.Row.Id, closed.Id);
            Assert.Equal(HandoffStatus.Done, closed.Status);
            Assert.Equal(clock.Now, closed.DoneAt);

            clock.Now += TimeSpan.FromMinutes(1);
            var again = await store.AskAsync(conversationId, HandoffAskedBy.Bot, "still stuck", Cancel);

            Assert.NotEqual(first.Row.Id, again.Row.Id);
            Assert.Equal(1, again.Position);
            Assert.Equal(2, await database.Handoffs.CountAsync(h => h.ConversationId == conversationId, Cancel));

            var open = await store.LatestAsync(conversationId, Cancel);

            Assert.NotNull(open);
            Assert.Equal(again.Row.Id, open.Id);

            Assert.False(await store.DoneAsync(NewConversationId(), Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task PositionMovesWhenAnEarlierChatCloses()
    {
        var conversationIds = new[] { NewConversationId(), NewConversationId(), NewConversationId() };
        foreach (var conversationId in conversationIds)
        {
            await fixture.MakeConversationAsync(conversationId);
        }

        try
        {
            await using var database = fixture.Open();
            var clock = new TestTimeProvider(Start);
            var store = new HandoffStore(database, clock);

            foreach (var conversationId in conversationIds)
            {
                await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel);
                clock.Now += TimeSpan.FromMinutes(1);
            }

            Assert.Equal(3, await store.PositionAsync(conversationIds[2], Cancel));

            Assert.True(await store.DoneAsync(conversationIds[0], Cancel));

            Assert.Null(await store.PositionAsync(conversationIds[0], Cancel));
            Assert.Equal(1, await store.PositionAsync(conversationIds[1], Cancel));
            Assert.Equal(2, await store.PositionAsync(conversationIds[2], Cancel));
        }
        finally
        {
            foreach (var conversationId in conversationIds)
            {
                await fixture.DeleteConversationAsync(conversationId);
            }
        }
    }

    [Fact]
    public async Task EmailLandsOnTheOpenRow()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var store = new HandoffStore(database, new TestTimeProvider(Start));

            await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel);

            Assert.True(await store.SetEmailAsync(conversationId, "visitor@example.com", Cancel));

            var row = await store.OpenAsync(conversationId, Cancel);

            Assert.NotNull(row);
            Assert.Equal("visitor@example.com", row.Email);

            Assert.False(await store.SetEmailAsync(NewConversationId(), "visitor@example.com", Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task ListReturnsTheQueueOldestFirst()
    {
        var conversationIds = new[] { NewConversationId(), NewConversationId(), NewConversationId() };
        foreach (var conversationId in conversationIds)
        {
            await fixture.MakeConversationAsync(conversationId);
        }

        try
        {
            await using var database = fixture.Open();
            var clock = new TestTimeProvider(Start);
            var store = new HandoffStore(database, clock);

            var expected = new List<long>();
            foreach (var conversationId in conversationIds)
            {
                expected.Add((await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, Cancel)).Row.Id);
                clock.Now += TimeSpan.FromMinutes(1);
            }

            var queue = await store.ListAsync(HandoffStatus.Waiting, 10, Cancel);

            Assert.Equal(expected, queue.Where(h => conversationIds.Contains(h.ConversationId)).Select(h => h.Id));
        }
        finally
        {
            foreach (var conversationId in conversationIds)
            {
                await fixture.DeleteConversationAsync(conversationId);
            }
        }
    }

    private static string NewConversationId() => "test-" + Guid.NewGuid().ToString("N");
}
