using System.Data.Common;
using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;
using SpiritAI.Database;
using SpiritAI.Settings;
using SpiritAI.Tests.Database;
using SpiritAI.Tests.Settings;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>The one writer and the guards of access spec section 4.5 and rulings R5, R6.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccessWriterTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SetRoles_ReplacesThem_AndFoldsRepeats()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.GrantAsync(personId, Permission.LookupUnits);
        var tech = await world.SeedRoleAsync("Technician", Permission.ChatAgentStaff);

        var result = await WriteAsync(world, writer => writer.SetRolesAsync(personId, [tech, tech], world.Admin, Cancel));

        Assert.Equal(AccessWrite.Done, result);
        Assert.Equal([tech], (await world.RowAsync(personId)).Roles.Select(role => role.Id));
    }

    [Fact]
    public async Task SetRoles_WithARoleThatDoesNotExist_ChangesNothing()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        var kept = await world.GrantAsync(personId, Permission.LookupUnits);

        var result = await WriteAsync(world, writer => writer.SetRolesAsync(personId, [kept, Guid.NewGuid()], world.Admin, Cancel));

        Assert.Equal(AccessWrite.UnknownRoles, result);
        Assert.Equal([kept], (await world.RowAsync(personId)).Roles.Select(role => role.Id));
    }

    [Fact]
    public async Task SetRoles_ForSomeoneNeonDoesNotHave_IsNoSuchPerson()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        Assert.Equal(AccessWrite.NoSuchPerson, await WriteAsync(world, writer => writer.SetRolesAsync(Guid.NewGuid(), [], world.Admin, Cancel)));
    }

    [Fact]
    public async Task TakingAdminFromTheLastAdmin_IsNoAdminLeft_AndChangesNothing()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: SettingsWorld.Everything);
        await world.RemoveEveryAdminAsync();
        var admin = await world.AddPersonAsync();
        await world.MakeAdminAsync(admin);

        var result = await WriteAsync(world, writer => writer.SetRolesAsync(admin, [], world.Admin, Cancel));

        Assert.Equal(AccessWrite.NoAdminLeft, result);
        Assert.Equal([AdminRole.Id], (await world.RowAsync(admin)).Roles.Select(role => role.Id));
    }

    [Fact]
    public async Task BanningTheLastAdmin_IsNoAdminLeft()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: SettingsWorld.Everything);
        await world.RemoveEveryAdminAsync();
        var admin = await world.AddPersonAsync();
        await world.MakeAdminAsync(admin);

        Assert.Equal(AccessWrite.NoAdminLeft, await WriteAsync(world, writer => writer.BanAsync(admin, null, world.Admin, Cancel)));
        Assert.False((await world.RowAsync(admin)).Banned);
    }

    [Fact]
    public async Task ABannedAdmin_DoesNotCountAsAnAdmin()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: SettingsWorld.Everything);
        await world.RemoveEveryAdminAsync();
        var active = await world.AddPersonAsync();
        var banned = await world.AddPersonAsync();
        await world.MakeAdminAsync(active);
        await world.MakeAdminAsync(banned);
        await world.BanAsync(banned);

        Assert.Equal(AccessWrite.NoAdminLeft, await WriteAsync(world, writer => writer.SetRolesAsync(active, [], world.Admin, Cancel)));
    }

    [Fact]
    public async Task TwoAdminsDemotingEachOtherAtOnce_LeaveOneAdmin()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: SettingsWorld.Everything);
        await world.RemoveEveryAdminAsync();
        var a = await world.AddPersonAsync();
        var b = await world.AddPersonAsync();
        await world.MakeAdminAsync(a);
        await world.MakeAdminAsync(b);
        var everything = SettingsWorld.Everything.ToHashSet();

        var results = await Task.WhenAll(
            WriteAsync(world, writer => writer.SetRolesAsync(b, [], new Caller(a, everything), Cancel)),
            WriteAsync(world, writer => writer.SetRolesAsync(a, [], new Caller(b, everything), Cancel)));

        Assert.Equal([AccessWrite.Done, AccessWrite.NoAdminLeft], results.Order());
        await using var db = fixture.Open();
        Assert.Equal(1, await db.UserRoles.CountAsync(grant => grant.RoleId == AdminRole.Id, Cancel));
    }

    [Fact]
    public async Task RemovingYourOwnAdmin_IsOwnAdmin_EvenWithAnotherAdmin()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        await world.MakeAdminAsync(await world.AddPersonAsync());

        Assert.Equal(AccessWrite.OwnAdmin, await WriteAsync(world, writer => writer.SetRolesAsync(world.CallerId, [], world.Admin, Cancel)));
    }

    [Fact]
    public async Task BanningYourself_IsNotYourOwn()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        Assert.Equal(AccessWrite.NotYourOwn, await WriteAsync(world, writer => writer.BanAsync(world.CallerId, null, world.Admin, Cancel)));
    }

    [Fact]
    public async Task ANonAdmin_CannotGiveTheAdminRole()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsPeople]);
        var caller = new Caller(world.CallerId, new HashSet<Permission> { Permission.SettingsPeople });

        var result = await WriteAsync(world, writer => writer.SetRolesAsync(world.CallerId, [AdminRole.Id], caller, Cancel));

        Assert.Equal(AccessWrite.BeyondYourAccess, result);
    }

    [Fact]
    public async Task ANonAdmin_CannotGiveARoleBeyondTheirOwnPermissions()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsPeople, Permission.LookupOrders]);
        var caller = new Caller(world.CallerId, new HashSet<Permission> { Permission.SettingsPeople, Permission.LookupOrders });
        var personId = await world.AddPersonAsync();
        var desk = await world.SeedRoleAsync("Orders", Permission.LookupOrders);
        var lookup = await world.SeedRoleAsync("Lookup", Permission.LookupUnits);

        Assert.Equal(AccessWrite.Done, await WriteAsync(world, writer => writer.SetRolesAsync(personId, [desk], caller, Cancel)));
        Assert.Equal(AccessWrite.BeyondYourAccess, await WriteAsync(world, writer => writer.SetRolesAsync(personId, [desk, lookup], caller, Cancel)));
        Assert.Equal(AccessWrite.BeyondYourAccess, await WriteAsync(world, writer => writer.CheckRolesAsync([lookup], caller, Cancel)));
    }

    [Fact]
    public async Task Ban_ThenUnban_WritesAndRemovesTheRow()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();

        Assert.Equal(AccessWrite.Done, await WriteAsync(world, writer => writer.BanAsync(personId, "Left", world.Admin, Cancel)));
        await using (var db = fixture.Open())
        {
            var ban = await db.PersonBans.SingleAsync(row => row.UserId == personId, Cancel);
            Assert.Equal((world.CallerId, "Left"), (ban.BannedBy!.Value, ban.Reason));
        }

        Assert.Equal(AccessWrite.Done, await WriteAsync(world, writer => writer.UnbanAsync(personId, world.Admin, Cancel)));
        Assert.False((await world.RowAsync(personId)).Banned);
    }

    [Fact]
    public async Task ANonAdmin_CannotUnbanAnAdmin()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsPeople]);
        var caller = new Caller(world.CallerId, new HashSet<Permission> { Permission.SettingsPeople });
        var admin = await world.AddPersonAsync();
        await world.MakeAdminAsync(admin);
        await world.BanAsync(admin);

        Assert.Equal(AccessWrite.BeyondYourAccess, await WriteAsync(world, writer => writer.UnbanAsync(admin, caller, Cancel)));
        Assert.True((await world.RowAsync(admin)).Banned);
    }

    [Fact]
    public async Task TheBuiltInRole_CannotBeEditedOrDeleted()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var draft = new RoleDraft("Admin", null, [Permission.LookupOrders]);

        Assert.Equal(AccessWrite.BuiltIn, await WriteAsync(world, writer => writer.UpdateRoleAsync(AdminRole.Id, draft, world.Admin, Cancel)));
        Assert.Equal(AccessWrite.BuiltIn, await WriteAsync(world, writer => writer.DeleteRoleAsync(AdminRole.Id, world.Admin, Cancel)));
    }

    [Fact]
    public async Task ARoleName_IsUniqueWithoutRegardToCase()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var name = $"Shift {SettingsWorld.Unique()}";

        var (first, _) = await CreateAsync(world, new RoleDraft(name, null, []));
        var (second, _) = await CreateAsync(world, new RoleDraft($"  {name.ToUpperInvariant()} ", null, []));

        Assert.Equal((AccessWrite.Done, AccessWrite.NameTaken), (first, second));
    }

    [Fact]
    public async Task SavingARole_DropsAKeyTheCodeNoLongerHas()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var roleId = await world.SeedRoleAsync("Old", Permission.LookupOrders);
        await using (var db = fixture.Open())
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, Key = "old.permission" });
            await db.SaveChangesAsync(Cancel);
        }

        var result = await WriteAsync(world, writer => writer.UpdateRoleAsync(roleId, new RoleDraft($"Old {SettingsWorld.Unique()}", "kept", [Permission.LookupOrders]), world.Admin, Cancel));

        Assert.Equal(AccessWrite.Done, result);

        await using var check = fixture.Open();
        Assert.Equal(["lookup.orders"], await check.RolePermissions.Where(p => p.RoleId == roleId).Select(p => p.Key).ToListAsync(Cancel));
    }

    [Fact]
    public async Task DeletingARole_TakesItsAccessAway()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        var roleId = await world.GrantAsync(personId, Permission.ChatAgentDealer);

        Assert.Equal(AccessWrite.Done, await WriteAsync(world, writer => writer.DeleteRoleAsync(roleId, world.Admin, Cancel)));

        Assert.Null((await world.RowAsync(personId)).Agent);
    }

    [Fact]
    public async Task ACallerWhoCancelsAfterTheCommit_StillGetsTheCacheDropped()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.GrantAsync(personId, Permission.SettingsPeople);
        var people = $"{SettingsEndpoints.Pattern}/people";
        Assert.Equal(HttpStatusCode.OK, (await world.GetAsAsync(personId, people)).StatusCode);

        using var request = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        await using var probe = fixture.Open();
        var options = new DbContextOptionsBuilder<SpiritDbContext>().AddInterceptors(new CancelOnCommit(request));
        options.UseSpiritNpgsql(probe.Database.GetConnectionString()!);
        await using var db = new SpiritDbContext(options.Options);
        await using var scope = world.Scope();
        var writer = new AccessWriter(
            db,
            scope.ServiceProvider.GetRequiredService<AccessCache>(),
            TimeProvider.System);

        var result = await writer.BanAsync(personId, null, world.Admin, request.Token);

        Assert.Equal(AccessWrite.Done, result);
        Assert.Equal(HttpStatusCode.Unauthorized, (await world.GetAsAsync(personId, people)).StatusCode);
    }

    private static async Task<AccessWrite> WriteAsync(SettingsWorld world, Func<AccessWriter, Task<AccessWrite>> write)
    {
        await using var scope = world.Scope();
        return await write(scope.ServiceProvider.GetRequiredService<AccessWriter>());
    }

    private static async Task<(AccessWrite, Guid)> CreateAsync(SettingsWorld world, RoleDraft draft)
    {
        await using var scope = world.Scope();
        return await scope.ServiceProvider.GetRequiredService<AccessWriter>().CreateRoleAsync(draft, world.Admin, Cancel);
    }

    /// <summary>Plays a caller who gives up the moment the transaction has committed.</summary>
    private sealed class CancelOnCommit(CancellationTokenSource request) : DbTransactionInterceptor
    {
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => await request.CancelAsync();
    }
}
