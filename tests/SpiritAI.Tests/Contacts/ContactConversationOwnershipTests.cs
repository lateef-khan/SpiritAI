using Microsoft.EntityFrameworkCore;

using SpiritAI.Contacts;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Contacts;

/// <summary>
/// <see cref="ContactConversationOwnership"/> against real PostgreSQL and the real
/// <see cref="ContactResolver"/>: the resolver a refused caller runs into must never write.
/// </summary>
public sealed class ContactConversationOwnershipTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ARefusedRequestLeavesTheContactsTableAsItWas()
    {
        var ownerKey = NewVisitorKey();
        var strangerKey = NewVisitorKey();
        var conversationId = "test-" + Guid.NewGuid().ToString("N");

        await fixture.MakeConversationAsync(conversationId);

        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, new TestTimeProvider(Start));
        var contactConversations = new ContactConversationStore(database, new TestTimeProvider(Start));

        var ownerId = await resolver.ResolveAsync(ownerKey, Cancel);
        await contactConversations.EnsureAsync(conversationId, ownerId, ContactChannel.Chat, Cancel);

        try
        {
            var strangerValue = ValueOf(strangerKey);
            var identitiesBefore = await database.ContactIdentities.CountAsync(i => i.Value == strangerValue, Cancel);

            var owns = await ContactConversationOwnership.OwnsAsync(contactConversations, resolver, conversationId, strangerKey, Cancel);

            Assert.False(owns);
            Assert.Equal(0, identitiesBefore);
            Assert.Equal(identitiesBefore, await database.ContactIdentities.CountAsync(i => i.Value == strangerValue, Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
            await CleanUpAsync(ownerKey);
        }
    }

    private async Task CleanUpAsync(string key)
    {
        await using var database = fixture.Open();
        var value = ValueOf(key);

        var contactIds = await database.ContactIdentities.AsNoTracking()
            .Where(i => i.Value == value)
            .Select(i => i.ContactId)
            .ToListAsync(Cancel);

        await database.ContactIdentities.Where(i => i.Value == value).ExecuteDeleteAsync(Cancel);

        await database.Contacts.Where(c => contactIds.Contains(c.Id)).ExecuteDeleteAsync(Cancel);
    }

    private static string NewVisitorKey() => "visitor:test-" + Guid.NewGuid().ToString("N");

    private static string ValueOf(string channelKey) => channelKey["visitor:".Length..];
}
