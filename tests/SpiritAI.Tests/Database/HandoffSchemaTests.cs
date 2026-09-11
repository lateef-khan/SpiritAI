using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Handoffs;

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
        var callId = NewCallId();
        await fixture.MakeCallAsync(callId);

        try
        {
            await using (var database = fixture.Open())
            {
                database.Handoffs.Add(Waiting(callId, HandoffAskedBy.Visitor));
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var won = await Task.WhenAll(ClaimAsync(callId, "staff:dana"), ClaimAsync(callId, "staff:sam"));

            Assert.Equal([0, 1], won.Order().ToArray());

            await using var reader = fixture.Open();
            var row = await reader.Handoffs.SingleAsync(h => h.CallId == callId, TestContext.Current.CancellationToken);

            Assert.Equal(HandoffStatus.Human, row.Status);
            Assert.Contains(row.AssigneeKey, new[] { "staff:dana", "staff:sam" });
            Assert.NotNull(row.ClaimedAt);
        }
        finally
        {
            await fixture.DeleteCallAsync(callId);
        }
    }

    [Fact]
    public async Task ASecondOpenRowForOneCallIsRefused()
    {
        var callId = NewCallId();
        await fixture.MakeCallAsync(callId);

        try
        {
            await using var database = fixture.Open();
            var first = Waiting(callId, HandoffAskedBy.Visitor);
            database.Handoffs.Add(first);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using (var second = fixture.Open())
            {
                second.Handoffs.Add(Waiting(callId, HandoffAskedBy.Bot));

                var refused = await Assert.ThrowsAsync<DbUpdateException>(
                    () => second.SaveChangesAsync(TestContext.Current.CancellationToken));

                var postgres = Assert.IsType<PostgresException>(refused.InnerException);
                Assert.Equal("handoff_open_per_call", postgres.ConstraintName);
            }

            first.Status = HandoffStatus.Done;
            first.DoneAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var third = fixture.Open();
            third.Handoffs.Add(Waiting(callId, HandoffAskedBy.Bot));
            await third.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, await third.Handoffs.CountAsync(h => h.CallId == callId, TestContext.Current.CancellationToken));
        }
        finally
        {
            await fixture.DeleteCallAsync(callId);
        }
    }

    /// <summary>The claim from the spec, on its own connection so two can race.</summary>
    /// <returns>How many rows the update touched: one for the winner, zero for the other.</returns>
    private async Task<int> ClaimAsync(string callId, string staffKey)
    {
        await using var database = fixture.Open();
        var now = DateTimeOffset.UtcNow;

        return await database.Handoffs
            .Where(h => h.CallId == callId && h.Status == HandoffStatus.Waiting && h.AssigneeKey == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(h => h.Status, HandoffStatus.Human)
                    .SetProperty(h => h.AssigneeKey, staffKey)
                    .SetProperty(h => h.ClaimedAt, now),
                TestContext.Current.CancellationToken);
    }

    private static Handoff Waiting(string callId, HandoffAskedBy askedBy)
        => new() { CallId = callId, Status = HandoffStatus.Waiting, AskedBy = askedBy };

    private static string NewCallId() => "test-" + Guid.NewGuid().ToString("N");
}
