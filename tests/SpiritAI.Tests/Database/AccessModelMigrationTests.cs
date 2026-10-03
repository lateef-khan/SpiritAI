using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using SpiritAI.Access;
using SpiritAI.Tests.Access;

using Xunit;

namespace SpiritAI.Tests.Database;

/// <summary>
/// A database at the schema before AccessModel, with one role per access group, migrates to the
/// permissions of access spec section 9, and every person keeps the access they had.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AccessModelMigrationTests(PostgresFixture fixture)
{
    private const string Units = "lookup.units";
    private const string Orders = "lookup.orders";

    private static readonly (string Group, string[] Keys)[] Groups =
    [
        ("Guest", ["chat.agent.guest"]),
        ("Dealer", ["chat.agent.dealer"]),
        ("TechService", ["chat.agent.staff", Units, Orders]),
        ("InsideSales", ["chat.agent.staff", Units, Orders]),
        ("InsideSalesSupervisor", ["chat.agent.staff", Units, Orders]),
        ("TechServiceManager", ["chat.agent.manager", Units, Orders]),
        ("InsideSalesManager", ["chat.agent.manager", Units, Orders]),
    ];

    private static readonly Dictionary<string, Guid> Users = new()
    {
        ["Guest"] = new("00000000-0000-4000-9000-000000000001"),
        ["Dealer"] = new("00000000-0000-4000-9000-000000000002"),
        ["TechService"] = new("00000000-0000-4000-9000-000000000003"),
        ["InsideSales"] = new("00000000-0000-4000-9000-000000000004"),
        ["InsideSalesSupervisor"] = new("00000000-0000-4000-9000-000000000005"),
        ["TechServiceManager"] = new("00000000-0000-4000-9000-000000000006"),
        ["InsideSalesManager"] = new("00000000-0000-4000-9000-000000000007"),
        ["Admin"] = new("00000000-0000-4000-9000-000000000008"),
        ["None"] = new("00000000-0000-4000-9000-000000000009"),
        ["Banned"] = new("00000000-0000-4000-9000-00000000000a"),
        ["NamedAdmin"] = new("00000000-0000-4000-9000-00000000000b"),
        ["AgentAdmin"] = new("00000000-0000-4000-9000-00000000000c"),
    };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryGroup_BecomesItsPermissions_AdminBecomesTheBuiltInRole_BansAreCopied_AndAnOldAdminNameStepsAside()
    {
        await using var scratch = await fixture.ScratchDatabaseAsync();
        await using (var db = scratch.Open())
        {
            await db.GetService<IMigrator>().MigrateAsync("LinkedUsers", Cancel);
            await SeedOldSchemaAsync(db);
            await db.Database.MigrateAsync(Cancel);
        }

        await using var check = scratch.Open();

        foreach (var (group, keys) in Groups)
        {
            var roleId = await check.Roles.Where(role => role.Name == $"{group} role").Select(role => role.Id).SingleAsync(Cancel);
            var stored = await check.RolePermissions.Where(p => p.RoleId == roleId).Select(p => p.Key).ToListAsync(Cancel);
            Assert.Equal(keys.Order(), stored.Order());

            var access = await TestResolver.ResolveAsync(scratch.Open, UserOf(group));
            Assert.Equal(keys.Order(), access.Held.Select(Permissions.KeyOf).Order());
        }

        foreach (var member in new[] { "Admin", "AgentAdmin" })
        {
            var admin = await TestResolver.ResolveAsync(scratch.Open, UserOf(member));
            Assert.Equal(9, admin.Held.Count);
            Assert.Equal(Permission.ChatAgentAdmin, admin.Agent);
            Assert.Equal(
                [AdminRole.Id],
                await check.UserRoles.Where(grant => grant.UserId == UserOf(member)).Select(grant => grant.RoleId).ToListAsync(Cancel));
        }

        Assert.False(await check.Roles.AnyAsync(role => role.Name == "AgentAdmin" || role.Name.StartsWith("Admin (old"), Cancel));
        var namedAdmin = await check.Roles.SingleAsync(role => role.Name.ToLower() == "admin", Cancel);
        Assert.Equal((AdminRole.Id, AdminRole.Name, true), (namedAdmin.Id, namedAdmin.Name, namedAdmin.BuiltIn));

        var none = await TestResolver.ResolveAsync(scratch.Open, UserOf("None"));
        Assert.Empty(none.Held);

        Assert.True((await TestResolver.ResolveAsync(scratch.Open, UserOf("Banned"))).Banned);
        Assert.Equal("Copied from Neon", (await check.PersonBans.SingleAsync(ban => ban.UserId == UserOf("Banned"), Cancel)).Reason);

        var renamed = await check.Roles
            .Join(check.UserRoles.Where(grant => grant.UserId == UserOf("NamedAdmin")), role => role.Id, grant => grant.RoleId, (role, _) => role)
            .SingleAsync(Cancel);
        Assert.Equal($"ADMIN (old {renamed.Id})", renamed.Name);
        Assert.False(renamed.BuiltIn);
    }

    private static Guid UserOf(string group) => Users[group];

    private static async Task SeedOldSchemaAsync(DbContext db)
    {
        string[] groups = [.. Groups.Select(row => row.Group)];

        foreach (var user in Users.Keys)
        {
            var id = UserOf(user);
            await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified", banned) VALUES ({id}, {user}, {user.ToLowerInvariant() + "@local.test"}, true, {user == "Banned"})""",
                Cancel);
        }

        foreach (var group in groups)
        {
            await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.role (name, access_group) VALUES ({group + " role"}, {group})""", Cancel);
            await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.user_role (user_id, role) VALUES ({UserOf(group)}, {group + " role"})""", Cancel);
        }

        await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.role (name, access_group) VALUES ('Admin', 'Admin'), ('AgentAdmin', 'Admin')""", Cancel);
        await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.user_role (user_id, role) VALUES ({UserOf("Admin")}, 'Admin'), ({UserOf("AgentAdmin")}, 'AgentAdmin')""", Cancel);

        await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.role (name, access_group) VALUES ('Warehouse', NULL)""", Cancel);
        await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.user_role (user_id, role) VALUES ({UserOf("None")}, 'Warehouse')""", Cancel);

        await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.role (name, access_group) VALUES ('ADMIN', NULL)""", Cancel);
        await db.Database.ExecuteSqlAsync($"""INSERT INTO spirit.user_role (user_id, role) VALUES ({UserOf("NamedAdmin")}, 'ADMIN')""", Cancel);
    }
}
