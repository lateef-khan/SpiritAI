using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using SpiritAI.Access;
using SpiritAI.Settings;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Settings;

/// <summary>The Roles page's routes (access spec sections 4.3 and 7.2, rulings R2, R3).</summary>
[Collection(PostgresCollection.Name)]
public sealed class RoleEndpointsTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string Roles = $"{SettingsEndpoints.Pattern}/roles";

    [Fact]
    public async Task ThePermissionList_IsTheSpecList_AgentsStrongestFirst_AndMarksTheAgents()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsPeople]);

        var raw = await (await world.GetAsync($"{SettingsEndpoints.Pattern}/permissions")).Content.ReadAsStringAsync(Cancel);
        using var wire = JsonDocument.Parse(raw);

        Assert.Equal(
            [
                "chat.agent.admin", "chat.agent.manager", "chat.agent.staff", "chat.agent.dealer", "chat.agent.guest",
                "lookup.units", "lookup.orders", "settings.people", "settings.roles",
            ],
            wire.RootElement.EnumerateArray().Select(row => row.GetProperty("key").GetString()));
        Assert.Equal(5, wire.RootElement.EnumerateArray().Count(row => row.GetProperty("agent").GetBoolean()));
    }

    [Fact]
    public async Task TheRoleList_HasTheBuiltInAdmin_HoldingEverything()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var roles = await (await world.GetAsync(Roles)).Content.ReadFromJsonAsync<IReadOnlyList<RoleRow>>(Cancel);

        var admin = roles!.Single(role => role.BuiltIn);
        Assert.Equal((AdminRole.Id, "Admin", 9), (admin.Id, admin.Name, admin.Permissions.Count));
        Assert.True(admin.Members >= 1);
    }

    [Fact]
    public async Task Create_ThenEdit_ThenDelete()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var name = $"Shift lead {SettingsWorld.Unique()}";

        var created = await (await world.PostJsonAsync(Roles, new { name, description = "Nights", permissions = new[] { "lookup.orders", "chat.agent.staff" } }))
            .Content.ReadFromJsonAsync<RoleRow>(Cancel);
        Assert.Equal([Permission.ChatAgentStaff, Permission.LookupOrders], created!.Permissions);

        var edited = await (await world.PutAsync($"{Roles}/{created.Id}", new { name = name + " 2", description = (string?)null, permissions = new[] { "lookup.units" } }))
            .Content.ReadFromJsonAsync<RoleRow>(Cancel);
        Assert.Equal((name + " 2", (string?)null), (edited!.Name, edited.Description));
        Assert.Equal([Permission.LookupUnits], edited.Permissions);

        Assert.Equal(HttpStatusCode.NoContent, (await world.DeleteAsync($"{Roles}/{created.Id}")).StatusCode);
        var after = await (await world.GetAsync(Roles)).Content.ReadFromJsonAsync<IReadOnlyList<RoleRow>>(Cancel);
        Assert.DoesNotContain(after!, role => role.Id == created.Id);
    }

    [Theory]
    [InlineData(" admin ")]
    [InlineData("ADMIN")]
    public async Task ARoleNamedLikeAdmin_InAnyCase_Is409(string name)
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.PostJsonAsync(Roles, new { name, description = (string?)null, permissions = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("A role with that name already exists.", await DetailAsync(response));
    }

    [Fact]
    public async Task TheBuiltInAdmin_CannotBeEditedOrDeleted()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var edit = await world.PutAsync($"{Roles}/{AdminRole.Id}", new { name = "Admin", description = (string?)null, permissions = Array.Empty<string>() });
        var delete = await world.DeleteAsync($"{Roles}/{AdminRole.Id}");

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (edit.StatusCode, delete.StatusCode));
        Assert.Equal("The Admin role cannot be changed.", await DetailAsync(edit));
        Assert.Equal("The Admin role cannot be changed.", await DetailAsync(delete));
    }

    [Fact]
    public async Task AnUnknownPermissionKey_Is400()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.PostJsonAsync(Roles, new { name = $"R {SettingsWorld.Unique()}", description = (string?)null, permissions = new[] { "chat.agent.root" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task APeopleOnlyCaller_ReadsRolesAndPermissions_ButCannotWriteThem()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsPeople]);

        Assert.Equal(HttpStatusCode.OK, (await world.GetAsync(Roles)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await world.PostJsonAsync(Roles, new { name = "x", description = (string?)null, permissions = Array.Empty<string>() })).StatusCode);
    }

    [Fact]
    public async Task ACallerWithNeitherSection_CannotReadRoles()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.LookupOrders]);

        Assert.Equal(HttpStatusCode.Forbidden, (await world.GetAsync(Roles)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.GetAsync($"{SettingsEndpoints.Pattern}/permissions")).StatusCode);
    }

    [Theory]
    [InlineData("""{"name":"R","permissions":[99]}""")]
    [InlineData("""{"name":"R"}""")]
    [InlineData("""{"name":"R","permissions":null}""")]
    [InlineData("""{"name":null,"permissions":[]}""")]
    public async Task AMalformedBody_Is400(string json)
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.SendJsonAsync(HttpMethod.Post, Roles, JsonDocument.Parse(json).RootElement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACaller_CannotCreateARoleHoldingAPermissionTheyLack()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsRoles]);

        var response = await world.PostJsonAsync(Roles, new { name = $"R {SettingsWorld.Unique()}", description = (string?)null, permissions = new[] { "lookup.units" } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("You can only give access you have yourself.", await DetailAsync(response));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenamingOntoAnotherRolesName_Is409(bool ontoAdmin)
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var other = $"Other {SettingsWorld.Unique()}";
        await world.PostJsonAsync(Roles, new { name = other, description = (string?)null, permissions = Array.Empty<string>() });
        var mine = await (await world.PostJsonAsync(Roles, new { name = $"Mine {SettingsWorld.Unique()}", description = (string?)null, permissions = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<RoleRow>(Cancel);

        var response = await world.PutAsync($"{Roles}/{mine!.Id}", new { name = ontoAdmin ? " ADMIN " : other.ToUpperInvariant(), description = (string?)null, permissions = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("A role with that name already exists.", await DetailAsync(response));
    }

    [Fact]
    public async Task EditingOrDeletingAnUnknownRole_Is404()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var unknown = Guid.NewGuid();

        var edit = await world.PutAsync($"{Roles}/{unknown}", new { name = "x", description = (string?)null, permissions = Array.Empty<string>() });
        var delete = await world.DeleteAsync($"{Roles}/{unknown}");

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (edit.StatusCode, delete.StatusCode));
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        return problem.RootElement.GetProperty("detail").GetString();
    }
}
