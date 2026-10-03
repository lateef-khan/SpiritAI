using System.Net;
using System.Text.Json;

using SpiritAI.Access;
using SpiritAI.Hub;
using SpiritAI.Settings;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Settings;

/// <summary>
/// Settings' routes through a real host: only <see cref="Permission.SettingsPeople"/> opens them (hub
/// spec, section 4.5; global constraints, Review Focus #5).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SettingsEndpointsTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ACallerWithoutSettingsPeople_Is403_OnBothRoutes()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.ChatAgentManager, Permission.SettingsRoles]);

        var list = await world.GetAsync($"{SettingsEndpoints.Pattern}/people");
        var link = await world.PostAsync($"{SettingsEndpoints.Pattern}/people/{Guid.NewGuid()}/desk");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, link.StatusCode);
    }

    [Fact]
    public async Task TheList_ShowsEveryPersonsAgentAndLinkStates()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var deskReady = await world.AddPersonAsync("Desk Ready");
        await world.GrantAsync(deskReady, Permission.ChatAgentStaff, Permission.LookupUnits);
        await world.SeedLinkAsync(deskReady, HubApps.Desk, SettingsWorld.Unique(), ready: true);

        var crmUnfinished = await world.AddPersonAsync("Crm Unfinished");
        await world.SeedLinkAsync(crmUnfinished, HubApps.Crm, SettingsWorld.Unique(), ready: false);

        var raw = await (await world.GetAsync($"{SettingsEndpoints.Pattern}/people")).Content.ReadAsStringAsync(Cancel);
        using var wire = JsonDocument.Parse(raw);
        Assert.Equal(
            "chat.agent.staff",
            wire.RootElement.EnumerateArray().Single(person => person.GetProperty("id").GetGuid() == deskReady).GetProperty("agent").GetString());

        var people = await world.PeopleAsync();

        var first = people.Single(person => person.Id == deskReady);
        Assert.Equal(Permission.ChatAgentStaff, first.Agent);
        Assert.Equal((LinkState.Ready, LinkState.None), (first.Desk, first.Crm));

        var second = people.Single(person => person.Id == crmUnfinished);
        Assert.Null(second.Agent);
        Assert.Equal((LinkState.None, LinkState.Unfinished), (second.Desk, second.Crm));
    }

    [Fact]
    public async Task TheList_ShowsRolesByIdAndName_AndTheBan()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync("Banned Tech");
        var tech = await world.SeedRoleAsync("Tech", Permission.ChatAgentStaff);
        var warehouse = await world.SeedRoleAsync("Warehouse");
        await world.GiveRoleAsync(personId, warehouse);
        await world.GiveRoleAsync(personId, tech);
        await world.BanAsync(personId);

        var row = await world.RowAsync(personId);

        // SeedRoleAsync names a role "<name> <unique suffix>"; the row lists roles by name.
        Assert.Collection(
            row.Roles,
            role => Assert.Equal((tech, true), (role.Id, role.Name.StartsWith("Tech ", StringComparison.Ordinal))),
            role => Assert.Equal((warehouse, true), (role.Id, role.Name.StartsWith("Warehouse ", StringComparison.Ordinal))));
        Assert.Equal(Permission.ChatAgentStaff, row.Agent);
        Assert.True(row.Banned);
    }

    [Fact]
    public async Task AnEmptyTwentyBaseUrl_Is503_WithTheHouseWords()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, twentyBaseUrl: string.Empty);
        var personId = await world.AddPersonAsync("No Crm Yet");

        try
        {
            var response = await world.PostAsync($"{SettingsEndpoints.Pattern}/people/{personId}/crm");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
            Assert.Equal("CRM is not set up yet.", problem.RootElement.GetProperty("detail").GetString());
        }
        finally
        {
            await world.DeletePersonAsync(personId);
        }
    }

    [Theory]
    [InlineData("desk")]
    [InlineData("crm")]
    public async Task LinkingABannedPerson_Is409_AndCallsNoApp(string app)
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.BanAsync(personId);

        var response = await world.PostAsync($"{SettingsEndpoints.Pattern}/people/{personId}/{app}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.Equal("This person is banned. Unban them first.", problem.RootElement.GetProperty("detail").GetString());
        Assert.Empty(world.DeskWire.Requests);
        Assert.Empty(world.CrmWire.Requests);
    }

    [Fact]
    public async Task ACrmThatDoesNotAnswer_Is503_SayingSo()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        world.CrmAnswers(HttpStatusCode.BadGateway);

        var response = await world.PostAsync($"{SettingsEndpoints.Pattern}/people/{personId}/crm");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.Equal("CRM did not answer.", problem.RootElement.GetProperty("detail").GetString());
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
}
