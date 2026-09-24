using Microsoft.EntityFrameworkCore;

using SpiritAI.Contacts;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Contacts;

/// <summary>
/// The flow of section 6.1 of the contact and identity design, move by move, against real PostgreSQL.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContactResolverTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnUnknownKeyMakesAContactAndAVerifiedVisitorIdentity()
    {
        var key = NewVisitorKey();

        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, new TestTimeProvider(Start));

        try
        {
            var contactId = await resolver.ResolveAsync(key, Cancel);

            var identity = await database.ContactIdentities.AsNoTracking()
                .SingleAsync(i => i.Kind == ContactIdentityKind.Visitor && i.Value == ValueOf(key), Cancel);

            Assert.Equal(contactId, identity.ContactId);
            Assert.True(identity.Verified);
        }
        finally
        {
            await CleanUpAsync(key);
        }
    }

    [Fact]
    public async Task TheSameKeyTwiceAnswersTheSameContact()
    {
        var key = NewVisitorKey();

        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, new TestTimeProvider(Start));

        try
        {
            var once = await resolver.ResolveAsync(key, Cancel);
            var twice = await resolver.ResolveAsync(key, Cancel);

            Assert.Equal(once, twice);
            Assert.Equal(1, await database.ContactIdentities.CountAsync(i => i.Value == ValueOf(key), Cancel));
        }
        finally
        {
            await CleanUpAsync(key);
        }
    }

    [Fact]
    public async Task ConcurrentFirstTimeResolvesOfOneKeyAnswerOneId()
    {
        var key = NewVisitorKey();

        try
        {
            var first = ResolveWithNewContextAsync(key);
            var second = ResolveWithNewContextAsync(key);

            var ids = await Task.WhenAll(first, second);

            Assert.Equal(ids[0], ids[1]);

            await using var database = fixture.Open();
            Assert.Equal(1, await database.ContactIdentities.CountAsync(i => i.Value == ValueOf(key), Cancel));
        }
        finally
        {
            await CleanUpAsync(key);
        }
    }

    [Fact]
    public async Task FindAnswersTheContactOfAKeyResolveAlreadySaw()
    {
        var key = NewVisitorKey();

        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, new TestTimeProvider(Start));

        try
        {
            var resolved = await resolver.ResolveAsync(key, Cancel);
            var found = await resolver.FindAsync(key, Cancel);

            Assert.Equal(resolved, found);
        }
        finally
        {
            await CleanUpAsync(key);
        }
    }

    [Fact]
    public async Task FindAnswersNullForAKeyNeverResolved()
    {
        var key = NewVisitorKey();

        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, new TestTimeProvider(Start));

        var found = await resolver.FindAsync(key, Cancel);

        Assert.Null(found);
        Assert.Equal(0, await database.ContactIdentities.CountAsync(i => i.Value == ValueOf(key), Cancel));
    }

    private async Task<long> ResolveWithNewContextAsync(string key)
    {
        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, new TestTimeProvider(Start));

        return await resolver.ResolveAsync(key, Cancel);
    }

    /// <summary>
    /// Deletes every identity this test made and every contact those identities pointed at. A
    /// contact orphaned by a lost race between two resolves of the same unknown key is left behind:
    /// nothing else here references it, and the throwaway database is wiped between runs.
    /// </summary>
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
