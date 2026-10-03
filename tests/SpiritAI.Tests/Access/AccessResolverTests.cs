using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SpiritAI.Access;
using SpiritAI.Hub;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>A person's access, read through their roles, the role permissions and the ban table.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccessResolverTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwoRoles_GiveTheUnionOfTheirPermissions_AndTheStrongestAgent()
    {
        var userId = await AddUserAsync();
        await GiveAsync(userId, Permission.ChatAgentDealer, Permission.LookupOrders);
        await GiveAsync(userId, Permission.ChatAgentStaff, Permission.LookupUnits);

        var access = await ResolveAsync(userId);

        Assert.Equal([Permission.ChatAgentStaff, Permission.ChatAgentDealer, Permission.LookupUnits, Permission.LookupOrders], access.Held);
        Assert.Equal(Permission.ChatAgentStaff, access.Agent);
        Assert.False(access.Banned);
    }

    [Fact]
    public async Task TheBuiltInAdminRole_HoldsEveryPermission()
    {
        var userId = await AddUserAsync();
        await using (var db = fixture.Open())
        {
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = AdminRole.Id });
            await db.SaveChangesAsync(Cancel);
        }

        var access = await ResolveAsync(userId);

        Assert.Equal(9, access.Held.Count);
        Assert.Equal(Permission.ChatAgentAdmin, access.Agent);
    }

    [Fact]
    public async Task AKeyTheCodeDoesNotKnow_IsIgnored_AndLoggedOnce()
    {
        var userId = await AddUserAsync();
        var roleId = await GiveAsync(userId, Permission.LookupUnits);
        var unknown = $"old.{Guid.NewGuid():N}";
        await using (var db = fixture.Open())
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, Key = unknown });
            await db.SaveChangesAsync(Cancel);
        }

        using var log = new CapturingLoggerProvider();
        var first = await ResolveAsync(userId, log);
        await ResolveAsync(userId, log);

        Assert.Equal([Permission.LookupUnits], first.Held);
        Assert.Single(log.Logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains(unknown, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ABanRow_IsBanned_AndKeepsThePermissions()
    {
        var userId = await AddUserAsync();
        await GiveAsync(userId, Permission.ChatAgentGuest);
        await using (var db = fixture.Open())
        {
            db.PersonBans.Add(new PersonBan { UserId = userId, BannedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Cancel);
        }

        var access = await ResolveAsync(userId);

        Assert.True(access.Banned);
        Assert.Equal([Permission.ChatAgentGuest], access.Held);
    }

    [Fact]
    public async Task AnUnknownUser_IsNotBanned_AndHoldsNothing()
    {
        var access = await ResolveAsync(Guid.NewGuid());

        Assert.False(access.Banned);
        Assert.Empty(access.Held);
        Assert.Null(access.Agent);
    }

    [Fact]
    public async Task DeletingTheNeonUser_DeletesTheirRolesBanAndLinks()
    {
        var userId = await AddUserAsync();
        await GiveAsync(userId, Permission.LookupOrders);
        await using (var db = fixture.Open())
        {
            db.PersonBans.Add(new PersonBan { UserId = userId, BannedAt = DateTimeOffset.UtcNow });
            db.LinkedUsers.Add(new LinkedUser { UserId = userId, App = HubApps.Crm, ExternalId = Guid.NewGuid().ToString(), Ready = true });
            await db.SaveChangesAsync(Cancel);
            await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {userId}""", Cancel);
        }

        await using var check = fixture.Open();
        Assert.False(await check.UserRoles.AnyAsync(grant => grant.UserId == userId, Cancel));
        Assert.False(await check.PersonBans.AnyAsync(ban => ban.UserId == userId, Cancel));
        Assert.False(await check.LinkedUsers.AnyAsync(link => link.UserId == userId, Cancel));
    }

    private async Task<Guid> AddUserAsync()
    {
        var userId = Guid.NewGuid();
        await using var db = fixture.Open();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({userId}, 'Test', {userId.ToString("N") + "@local.test"}, true)""",
            Cancel);
        return userId;
    }

    private async Task<Guid> GiveAsync(Guid userId, params Permission[] permissions)
    {
        var roleId = Guid.NewGuid();
        await using var db = fixture.Open();
        db.Roles.Add(new Role { Id = roleId, Name = $"role-{roleId:N}" });
        db.RolePermissions.AddRange(permissions.Select(permission => new RolePermission { RoleId = roleId, Key = Permissions.KeyOf(permission) }));
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync(Cancel);
        return roleId;
    }

    private Task<PersonAccess> ResolveAsync(Guid userId, ILoggerProvider? log = null)
        => TestResolver.ResolveAsync(fixture.Open, userId, log);
}
