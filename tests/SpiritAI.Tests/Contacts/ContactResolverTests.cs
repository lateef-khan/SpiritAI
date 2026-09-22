using Microsoft.EntityFrameworkCore;

using SpiritAI.Contacts;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Contacts;

/// <summary>
/// The flow of section 6.1 of the contact and identity design, move by move, against real PostgreSQL.
/// </summary>
public sealed class ContactResolverTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
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
    public async Task AMergedContactResolvesToTheSurvivor()
    {
        var key = NewVisitorKey();
        var clock = new TestTimeProvider(Start);

        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, clock);

        try
        {
            var loser = await resolver.ResolveAsync(key, Cancel);

            var survivor = new Contact { CreatedAt = clock.GetUtcNow() };
            database.Contacts.Add(survivor);
            await database.SaveChangesAsync(Cancel);

            await database.Contacts.Where(c => c.Id == loser)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.MergedInto, survivor.Id), Cancel);

            var resolved = await resolver.ResolveAsync(key, Cancel);

            Assert.Equal(survivor.Id, resolved);
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

    private async Task<long> ResolveWithNewContextAsync(string key)
    {
        await using var database = fixture.Open();
        var resolver = new ContactResolver(database, new TestTimeProvider(Start));

        return await resolver.ResolveAsync(key, Cancel);
    }

    /// <summary>
    /// Deletes every identity this test made and every contact those identities, or a merge made,
    /// pointed at. A contact orphaned by a lost race between two resolves of the same unknown key is
    /// left behind: nothing else here references it, and the throwaway database is wiped between runs.
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

        var survivors = await database.Contacts.AsNoTracking()
            .Where(c => contactIds.Contains(c.Id) && c.MergedInto != null)
            .Select(c => c.MergedInto!.Value)
            .ToListAsync(Cancel);

        await database.Contacts.Where(c => contactIds.Contains(c.Id) || survivors.Contains(c.Id)).ExecuteDeleteAsync(Cancel);
    }

    private static string NewVisitorKey() => "visitor:test-" + Guid.NewGuid().ToString("N");

    private static string ValueOf(string channelKey) => channelKey["visitor:".Length..];
}
