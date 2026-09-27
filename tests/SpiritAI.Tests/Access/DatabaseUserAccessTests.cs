using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>A user's groups, read through their roles in <c>spirit.user_role</c> and <c>spirit.role</c>.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DatabaseUserAccessTests(PostgresFixture fixture)
{
    [Fact]
    public async Task EveryRoleWithAGroup_GivesItsGroup_AndARoleWithNoneGivesNothing()
    {
        var userId = await AddUserAsync(
            ("TechService", AccessGroup.TechService),
            ("TechServiceManager", AccessGroup.TechServiceManager),
            ("Warehouse", null));

        var groups = await GroupsOfAsync(userId);

        Assert.Equal([AccessGroup.TechService, AccessGroup.TechServiceManager], groups.Order());
    }

    [Fact]
    public async Task OnlyRolesWithNoGroup_GiveNoGroup()
    {
        var userId = await AddUserAsync(("Warehouse", null), ("Receiving", null));

        Assert.Empty(await GroupsOfAsync(userId));
    }

    [Fact]
    public async Task NoRoles_GiveNoGroup()
    {
        var userId = await AddUserAsync();

        Assert.Empty(await GroupsOfAsync(userId));
    }

    [Fact]
    public async Task DeletingTheNeonUser_DeletesTheirRoles()
    {
        var userId = await AddUserAsync(("Admin", AccessGroup.Admin));

        await using (var db = fixture.Open())
        {
            await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {userId}""", TestContext.Current.CancellationToken);
        }

        await using var check = fixture.Open();
        Assert.False(await check.UserRoles.AnyAsync(grant => grant.UserId == userId, TestContext.Current.CancellationToken));
    }

    /// <summary>Adds a Neon user holding roles made for this test alone, so tests never share a row.</summary>
    private async Task<Guid> AddUserAsync(params (string Name, AccessGroup? Group)[] roles)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var db = fixture.Open();

        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({userId}, 'Test', {suffix + "@local.test"}, true)""",
            cancellationToken);

        foreach (var (name, group) in roles)
        {
            db.Roles.Add(new Role { Name = $"{name}-{suffix}", AccessGroup = group?.ToString() });
            db.UserRoles.Add(new UserRole { UserId = userId, Role = $"{name}-{suffix}" });
        }

        await db.SaveChangesAsync(cancellationToken);

        return userId;
    }

    private async Task<IReadOnlyList<AccessGroup>> GroupsOfAsync(Guid userId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAccess();
        services.AddScoped(_ => fixture.Open());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IUserAccess>()
            .GroupsOfAsync(userId, TestContext.Current.CancellationToken);
    }
}
