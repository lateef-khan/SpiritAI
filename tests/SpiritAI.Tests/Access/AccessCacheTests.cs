using System.Net;

using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;
using SpiritAI.Settings;
using SpiritAI.Tests.Database;
using SpiritAI.Tests.Settings;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>A write counts on the next request, for one person or for every holder of a role (access spec section 6.1).</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccessCacheTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ARoleEdit_ClearsEveryHoldersCache()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var first = await world.AddPersonAsync();
        var second = await world.AddPersonAsync();
        var roleId = await world.SeedRoleAsync("Office", Permission.SettingsPeople);
        await world.GiveRoleAsync(first, roleId);
        await world.GiveRoleAsync(second, roleId);
        var people = $"{SettingsEndpoints.Pattern}/people";

        Assert.Equal(HttpStatusCode.OK, (await world.GetAsAsync(first, people)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.GetAsAsync(second, people)).StatusCode);

        await using (var scope = world.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<AccessWriter>()
                .UpdateRoleAsync(roleId, new RoleDraft($"Office {SettingsWorld.Unique()}", null, [Permission.LookupOrders]), world.Admin, Cancel);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await world.GetAsAsync(first, people)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.GetAsAsync(second, people)).StatusCode);
    }

    [Fact]
    public async Task ABan_CountsOnThePersonsNextRequest()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.GrantAsync(personId, Permission.SettingsPeople);
        var people = $"{SettingsEndpoints.Pattern}/people";
        Assert.Equal(HttpStatusCode.OK, (await world.GetAsAsync(personId, people)).StatusCode);

        await using (var scope = world.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<AccessWriter>().BanAsync(personId, null, world.Admin, Cancel);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await world.GetAsAsync(personId, people)).StatusCode);
    }
}
