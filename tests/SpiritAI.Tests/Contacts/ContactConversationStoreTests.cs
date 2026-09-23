using Microsoft.EntityFrameworkCore;

using SpiritAI.Contacts;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Contacts;

/// <summary>
/// <c>spirit.contact_conversation</c> against real PostgreSQL: a widget turn writes the row once,
/// however many times, or however concurrently, it is asked to.
/// </summary>
public sealed class ContactConversationStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwoTurnsOnTheSameChatWriteOneRow()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);
        var contactId = await MakeContactAsync();

        try
        {
            await using var database = fixture.Open();
            var store = new ContactConversationStore(database, new TestTimeProvider(Start));

            await store.EnsureAsync(conversationId, contactId, ContactChannel.Chat, Cancel);
            await store.EnsureAsync(conversationId, contactId, ContactChannel.Chat, Cancel);

            Assert.Equal(1, await database.ContactConversations.CountAsync(c => c.ConversationId == conversationId, Cancel));

            var row = await database.ContactConversations.AsNoTracking()
                .SingleAsync(c => c.ConversationId == conversationId, Cancel);

            Assert.Equal(contactId, row.ContactId);
            Assert.Equal(ContactChannel.Chat, row.Channel);
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
            await DeleteContactAsync(contactId);
        }
    }

    [Fact]
    public async Task ConcurrentFirstWritesOnTheSameChatLeaveOneRow()
    {
        var conversationId = NewConversationId();
        await fixture.MakeConversationAsync(conversationId);
        var contactId = await MakeContactAsync();

        try
        {
            var first = EnsureWithNewContextAsync(conversationId, contactId);
            var second = EnsureWithNewContextAsync(conversationId, contactId);

            await Task.WhenAll(first, second);

            await using var database = fixture.Open();
            Assert.Equal(1, await database.ContactConversations.CountAsync(c => c.ConversationId == conversationId, Cancel));
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);
            await DeleteContactAsync(contactId);
        }
    }

    [Fact]
    public async Task TheLatestChatIsTheOneThatStartedLastOnThatChannel()
    {
        string[] conversationIds = [NewConversationId(), NewConversationId(), NewConversationId()];
        foreach (var conversationId in conversationIds)
        {
            await fixture.MakeConversationAsync(conversationId);
        }

        var contactId = await MakeContactAsync();
        var otherContactId = await MakeContactAsync();

        try
        {
            await using var database = fixture.Open();
            var (older, newer, phone) = (conversationIds[0], conversationIds[1], conversationIds[2]);

            await new ContactConversationStore(database, new TestTimeProvider(Start.AddDays(2))).EnsureAsync(newer, contactId, ContactChannel.Chat, Cancel);
            await new ContactConversationStore(database, new TestTimeProvider(Start)).EnsureAsync(older, contactId, ContactChannel.Chat, Cancel);
            await new ContactConversationStore(database, new TestTimeProvider(Start.AddDays(3))).EnsureAsync(phone, contactId, ContactChannel.Phone, Cancel);

            var store = new ContactConversationStore(database, new TestTimeProvider(Start));

            Assert.Equal(newer, await store.LatestAsync(contactId, ContactChannel.Chat, Cancel));
            Assert.Null(await store.LatestAsync(otherContactId, ContactChannel.Chat, Cancel));
        }
        finally
        {
            foreach (var conversationId in conversationIds)
            {
                await fixture.DeleteConversationAsync(conversationId);
            }

            await DeleteContactAsync(contactId);
            await DeleteContactAsync(otherContactId);
        }
    }

    private async Task EnsureWithNewContextAsync(string conversationId, long contactId)
    {
        await using var database = fixture.Open();
        var store = new ContactConversationStore(database, new TestTimeProvider(Start));

        await store.EnsureAsync(conversationId, contactId, ContactChannel.Chat, Cancel);
    }

    private async Task<long> MakeContactAsync()
    {
        await using var database = fixture.Open();
        var contact = new Contact { CreatedAt = Start };
        database.Contacts.Add(contact);
        await database.SaveChangesAsync(Cancel);

        return contact.Id;
    }

    private async Task DeleteContactAsync(long contactId)
    {
        await using var database = fixture.Open();
        await database.Contacts.Where(c => c.Id == contactId).ExecuteDeleteAsync(Cancel);
    }

    private static string NewConversationId() => "test-" + Guid.NewGuid().ToString("N");
}
