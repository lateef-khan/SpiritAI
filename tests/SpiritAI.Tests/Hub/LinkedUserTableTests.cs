using Microsoft.EntityFrameworkCore;

using SpiritAI.Hub;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Hub;

/// <summary><c>spirit.linked_user</c> against the rules in the hub spec, section 4.2.</summary>
[Collection(PostgresCollection.Name)]
public sealed class LinkedUserTableTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeletingTheNeonUser_DeletesTheirLinks()
    {
        var userId = await AddNeonUserAsync();
        await using (var db = fixture.Open())
        {
            db.LinkedUsers.Add(new LinkedUser { UserId = userId, App = HubApps.Desk, ExternalId = Unique(), Ready = true });
            await db.SaveChangesAsync(Cancel);
            await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {userId}""", Cancel);
        }

        await using var check = fixture.Open();
        Assert.False(await check.LinkedUsers.AnyAsync(l => l.UserId == userId, Cancel));
    }

    [Fact]
    public async Task OneAppUser_CannotBeLinkedToTwoPeople()
    {
        var first = await AddNeonUserAsync();
        var second = await AddNeonUserAsync();
        var shared = Unique();

        await using var db = fixture.Open();
        db.LinkedUsers.Add(new LinkedUser { UserId = first, App = HubApps.Desk, ExternalId = shared, Ready = true });
        await db.SaveChangesAsync(Cancel);
        db.LinkedUsers.Add(new LinkedUser { UserId = second, App = HubApps.Desk, ExternalId = shared, Ready = true });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Cancel));
    }

    [Fact]
    public async Task AnAppOtherThanDeskOrCrm_IsRefused()
    {
        var userId = await AddNeonUserAsync();

        await using var db = fixture.Open();
        db.LinkedUsers.Add(new LinkedUser { UserId = userId, App = "mail", ExternalId = Unique(), Ready = true });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Cancel));
    }

    /// <summary>Adds a Neon user made for this test alone, so tests never share a row.</summary>
    private async Task<Guid> AddNeonUserAsync()
    {
        var userId = Guid.NewGuid();

        await using var db = fixture.Open();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({userId}, 'Test', {userId.ToString("N") + "@local.test"}, true)""",
            Cancel);

        return userId;
    }

    private static string Unique() => Guid.NewGuid().ToString("N");
}
