using Microsoft.EntityFrameworkCore;

using SpiritAI.Contacts;
using SpiritAI.Database.Migrations;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Database.Migrations;

/// <summary>
/// <see cref="BackfillContactConversations.BackfillSql"/> against real PostgreSQL: run it once, and
/// run it again, against a conversation the backfill never saw. It ships already applied through
/// <see cref="PostgresFixture"/>'s own migrate, so a test asks for a widget conversation of its own
/// and runs the same SQL by hand.
/// </summary>
public sealed class BackfillContactConversationsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AVisitorOwnedConversationGetsOneContactOneIdentityOneRow()
    {
        var visitorKey = "visitor:" + Guid.NewGuid().ToString("N");
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId, visitorKey, Start);

        try
        {
            await using (var database = fixture.Open())
            {
                await database.Database.ExecuteSqlRawAsync(BackfillContactConversations.BackfillSql, Cancel);
            }

            await using var read = fixture.Open();
            var value = visitorKey["visitor:".Length..];

            var identity = await read.ContactIdentities.AsNoTracking()
                .SingleAsync(i => i.Kind == ContactIdentityKind.Visitor && i.Value == value, Cancel);
            Assert.True(identity.Verified);

            var row = await read.ContactConversations.AsNoTracking()
                .SingleAsync(c => c.ConversationId == conversationId, Cancel);
            Assert.Equal(identity.ContactId, row.ContactId);
            Assert.Equal(ContactChannel.Chat, row.Channel);
            Assert.Equal(Start, row.StartedAt);
        }
        finally
        {
            await CleanUpAsync(conversationId, visitorKey);
        }
    }

    [Fact]
    public async Task TwoConversationsForOneVisitorShareOneContact()
    {
        var visitorKey = "visitor:" + Guid.NewGuid().ToString("N");
        var first = NewConversationId();
        var second = NewConversationId();
        await fixture.MakeConversationAsync(first, visitorKey, Start);
        await fixture.MakeConversationAsync(second, visitorKey, Start.AddMinutes(5));

        try
        {
            await using (var database = fixture.Open())
            {
                await database.Database.ExecuteSqlRawAsync(BackfillContactConversations.BackfillSql, Cancel);
            }

            await using var read = fixture.Open();
            var value = visitorKey["visitor:".Length..];

            Assert.Equal(1, await read.ContactIdentities.CountAsync(i => i.Kind == ContactIdentityKind.Visitor && i.Value == value, Cancel));

            var rows = await read.ContactConversations.AsNoTracking()
                .Where(c => c.ConversationId == first || c.ConversationId == second)
                .ToListAsync(Cancel);

            Assert.Equal(2, rows.Count);
            Assert.Equal(rows[0].ContactId, rows[1].ContactId);
        }
        finally
        {
            // The contact's row is still referenced by whichever conversation is cleaned up last,
            // so both conversations go first and the shared contact goes once, after them.
            await fixture.DeleteConversationAsync(second);
            await CleanUpAsync(first, visitorKey);
        }
    }

    [Fact]
    public async Task RunningItAgainChangesNothing()
    {
        var visitorKey = "visitor:" + Guid.NewGuid().ToString("N");
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId, visitorKey, Start);

        try
        {
            await using (var database = fixture.Open())
            {
                await database.Database.ExecuteSqlRawAsync(BackfillContactConversations.BackfillSql, Cancel);
                await database.Database.ExecuteSqlRawAsync(BackfillContactConversations.BackfillSql, Cancel);
            }

            await using var read = fixture.Open();
            var value = visitorKey["visitor:".Length..];

            Assert.Equal(1, await read.ContactIdentities.CountAsync(i => i.Kind == ContactIdentityKind.Visitor && i.Value == value, Cancel));
            Assert.Equal(1, await read.ContactConversations.CountAsync(c => c.ConversationId == conversationId, Cancel));
        }
        finally
        {
            await CleanUpAsync(conversationId, visitorKey);
        }
    }

    private static string NewConversationId() => "backfill-" + Guid.NewGuid().ToString("N");

    private async Task CleanUpAsync(string conversationId, string visitorKey)
    {
        await fixture.DeleteConversationAsync(conversationId);

        await using var database = fixture.Open();
        var value = visitorKey["visitor:".Length..];

        var contactIds = await database.ContactIdentities.AsNoTracking()
            .Where(i => i.Kind == ContactIdentityKind.Visitor && i.Value == value)
            .Select(i => i.ContactId)
            .ToListAsync(Cancel);

        await database.ContactIdentities.Where(i => i.Kind == ContactIdentityKind.Visitor && i.Value == value).ExecuteDeleteAsync(Cancel);
        await database.Contacts.Where(c => contactIds.Contains(c.Id)).ExecuteDeleteAsync(Cancel);
    }
}
