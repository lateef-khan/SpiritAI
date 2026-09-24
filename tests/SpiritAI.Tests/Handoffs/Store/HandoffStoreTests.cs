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
[Collection(PostgresCollection.Name)]
public sealed class HandoffStoreTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AskMakesAWaitingRow()
    {
        var (first, second) = (NewConversationId(), NewConversationId());
        await fixture.MakeConversationAsync(first);
        await fixture.MakeConversationAsync(second);

        try
        {
            await using var database = fixture.Open();
            var clock = new TestTimeProvider(Start);
            var store = new HandoffStore(database, clock);

            var a = await store.AskAsync(first, HandoffAskedBy.Visitor, null, HandoffSummary.Empty, Cancel);
            clock.Now += TimeSpan.FromMinutes(1);
            var b = await store.AskAsync(second, HandoffAskedBy.Bot, "warranty claim", HandoffSummary.Empty, Cancel);

            Assert.Equal(HandoffStatus.Waiting, a.Status);
            Assert.Equal(HandoffStatus.Waiting, b.Status);
            Assert.Equal(HandoffAskedBy.Visitor, a.AskedBy);
            Assert.Null(a.Reason);
            Assert.Equal(HandoffAskedBy.Bot, b.AskedBy);
            Assert.Equal("warranty claim", b.Reason);
            Assert.Equal(Start, a.AskedAt);
            Assert.Equal(Start.AddMinutes(1), b.AskedAt);
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

            var once = await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, HandoffSummary.Empty, Cancel);
            var twice = await store.AskAsync(conversationId, HandoffAskedBy.Bot, "again", HandoffSummary.Empty, Cancel);

            Assert.Equal(once.Id, twice.Id);
            Assert.Equal(HandoffAskedBy.Visitor, twice.AskedBy);
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

            await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, HandoffSummary.Empty, Cancel);
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
    public async Task OnlyAHeldChatIsHandedOver()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var store = new HandoffStore(database, new TestTimeProvider(Start));

            await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, HandoffSummary.Empty, Cancel);

            Assert.False(await store.HandOverAsync(conversationId, "staff:sam", "Sam", Cancel));

            await store.ClaimAsync(conversationId, "staff:dana", "Dana R.", Cancel);

            Assert.False(await store.HandOverAsync(conversationId, "staff:dana", "Dana R.", Cancel));
            Assert.True(await store.HandOverAsync(conversationId, "staff:sam", "Sam", Cancel));

            var open = await store.OpenAsync(conversationId, Cancel);
            Assert.NotNull(open);
            Assert.Equal(HandoffStatus.Human, open.Status);
            Assert.Equal(("staff:sam", "Sam"), (open.AssigneeKey, open.AssigneeName));

            await store.DoneAsync(conversationId, Cancel);

            Assert.False(await store.HandOverAsync(conversationId, "staff:dana", "Dana R.", Cancel));
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

            var first = await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, HandoffSummary.Empty, Cancel);
            clock.Now += TimeSpan.FromMinutes(1);

            Assert.True(await store.DoneAsync(conversationId, Cancel));
            Assert.Null(await store.OpenAsync(conversationId, Cancel));

            var closed = await store.LatestAsync(conversationId, Cancel);

            Assert.NotNull(closed);
            Assert.Equal(first.Id, closed.Id);
            Assert.Equal(HandoffStatus.Done, closed.Status);
            Assert.Equal(clock.Now, closed.DoneAt);

            clock.Now += TimeSpan.FromMinutes(1);
            var again = await store.AskAsync(conversationId, HandoffAskedBy.Bot, "still stuck", HandoffSummary.Empty, Cancel);

            Assert.NotEqual(first.Id, again.Id);
            Assert.Equal(2, await database.Handoffs.CountAsync(h => h.ConversationId == conversationId, Cancel));

            var open = await store.LatestAsync(conversationId, Cancel);

            Assert.NotNull(open);
            Assert.Equal(again.Id, open.Id);

            Assert.False(await store.DoneAsync(NewConversationId(), Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task PhoneLandsOnTheOpenRow()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var store = new HandoffStore(database, new TestTimeProvider(Start));

            await store.AskAsync(conversationId, HandoffAskedBy.Visitor, null, HandoffSummary.Empty, Cancel);

            Assert.True(await store.SetPhoneAsync(conversationId, "+12015550123", Cancel));

            var row = await store.OpenAsync(conversationId, Cancel);

            Assert.NotNull(row);
            Assert.Equal("+12015550123", row.Phone);

            Assert.False(await store.SetPhoneAsync(NewConversationId(), "+12015550123", Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task TheSummaryIsSavedWithTheAsk()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var store = new HandoffStore(database, new TestTimeProvider(Start));
            var summary = new HandoffSummary("XT485 treadmill", "0045210000001234", "Lubricated the belt; no change", "A technician visit");

            var asked = await store.AskAsync(conversationId, HandoffAskedBy.Bot, "Belt slips at speed 6", summary, Cancel);

            var row = await store.OpenAsync(conversationId, Cancel);

            Assert.NotNull(row);
            Assert.Equal(asked.Id, row.Id);
            Assert.Equal("XT485 treadmill", row.Product);
            Assert.Equal("0045210000001234", row.Serial);
            Assert.Equal("Lubricated the belt; no change", row.Tried);
            Assert.Equal("A technician visit", row.Wants);
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task ASecondAskFillsOnlyThePartsStillEmpty()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var store = new HandoffStore(database, new TestTimeProvider(Start));

            var once = await store.AskAsync(
                conversationId, HandoffAskedBy.Bot, "Belt slips", new HandoffSummary("XT485 treadmill", null, null, null), Cancel);

            var twice = await store.AskAsync(
                conversationId,
                HandoffAskedBy.Bot,
                "Belt slips",
                new HandoffSummary("F80 treadmill", "0045210000001234", "Lubricated the belt", null),
                Cancel);

            Assert.Equal(once.Id, twice.Id);
            Assert.Equal("XT485 treadmill", twice.Product);
            Assert.Equal("0045210000001234", twice.Serial);
            Assert.Equal("Lubricated the belt", twice.Tried);
            Assert.Null(twice.Wants);

            var row = await store.OpenAsync(conversationId, Cancel);

            Assert.NotNull(row);
            Assert.Equal("XT485 treadmill", row.Product);
            Assert.Equal("0045210000001234", row.Serial);
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    private static string NewConversationId() => "test-" + Guid.NewGuid().ToString("N");
}
