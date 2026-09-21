using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Handoffs.Model;

using Xunit;

namespace SpiritAI.Tests.Database;

/// <summary>
/// The two facts the <c>spirit.handoff</c> table promises that only PostgreSQL can keep: a claim
/// is atomic across any number of machines, and a chat has at most one open handoff.
/// </summary>
public sealed class HandoffSchemaTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task TwoClaimsOnOneRowOnlyOneWins()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using (var database = fixture.Open())
            {
                database.Handoffs.Add(Waiting(conversationId, HandoffAskedBy.Visitor));
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var won = await Task.WhenAll(ClaimAsync(conversationId, "staff:dana"), ClaimAsync(conversationId, "staff:sam"));

            Assert.Equal([0, 1], won.Order().ToArray());

            await using var reader = fixture.Open();
            var row = await reader.Handoffs.SingleAsync(h => h.ConversationId == conversationId, TestContext.Current.CancellationToken);

            Assert.Equal(HandoffStatus.Human, row.Status);
            Assert.Contains(row.AssigneeKey, new[] { "staff:dana", "staff:sam" });
            Assert.NotNull(row.ClaimedAt);
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    [Fact]
    public async Task ASecondOpenRowForOneConversationIsRefused()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);

        try
        {
            await using var database = fixture.Open();
            var first = Waiting(conversationId, HandoffAskedBy.Visitor);
            database.Handoffs.Add(first);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using (var second = fixture.Open())
            {
                second.Handoffs.Add(Waiting(conversationId, HandoffAskedBy.Bot));

                var refused = await Assert.ThrowsAsync<DbUpdateException>(
                    () => second.SaveChangesAsync(TestContext.Current.CancellationToken));

                var postgres = Assert.IsType<PostgresException>(refused.InnerException);
                Assert.Equal("handoff_open_per_conversation", postgres.ConstraintName);
            }

            first.Status = HandoffStatus.Done;
            first.DoneAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var third = fixture.Open();
            third.Handoffs.Add(Waiting(conversationId, HandoffAskedBy.Bot));
            await third.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, await third.Handoffs.CountAsync(h => h.ConversationId == conversationId, TestContext.Current.CancellationToken));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
        }
    }

    /// <summary>The claim from the spec, on its own connection so two can race.</summary>
    /// <returns>How many rows the update touched: one for the winner, zero for the other.</returns>
    private async Task<int> ClaimAsync(string conversationId, string staffKey)
    {
        await using var database = fixture.Open();
        var now = DateTimeOffset.UtcNow;

        return await database.Handoffs
            .Where(h => h.ConversationId == conversationId && h.Status == HandoffStatus.Waiting && h.AssigneeKey == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(h => h.Status, HandoffStatus.Human)
                    .SetProperty(h => h.AssigneeKey, staffKey)
                    .SetProperty(h => h.ClaimedAt, now),
                TestContext.Current.CancellationToken);
    }

    private static Handoff Waiting(string conversationId, HandoffAskedBy askedBy)
        => new() { ConversationId = conversationId, Status = HandoffStatus.Waiting, AskedBy = askedBy };

    private static string NewConversationId() => "test-" + Guid.NewGuid().ToString("N");
}
