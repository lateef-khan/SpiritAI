using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using SpiritAI.Access;
using SpiritAI.Hub;
using SpiritAI.Settings;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Settings;

/// <summary>Add, roles, ban and unban, each one server call (access spec section 7.2).</summary>
[Collection(PostgresCollection.Name)]
public sealed class PeopleEndpointsTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string People = $"{SettingsEndpoints.Pattern}/people";

    /// <summary>The Chatwoot user id in <c>Hub/Payloads/platform_user_created.json</c>.</summary>
    private const string CreatedDeskUser = "4";

    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "" }, { "POST", "" }, { "PUT", "/{0}/roles" }, { "POST", "/{0}/ban" },
        { "POST", "/{0}/unban" }, { "DELETE", "/{0}" }, { "POST", "/{0}/desk" }, { "DELETE", "/{0}/desk" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryPeopleRoute_NeedsSettingsPeople(string method, string path)
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsRoles]);

        var response = await world.SendJsonAsync(new HttpMethod(method), People + string.Format(System.Globalization.CultureInfo.InvariantCulture, path, Guid.NewGuid()), new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Add_MakesTheNeonUser_SavesTheRoles_AndLinksDesk()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        await world.DeleteDeskLinkAsync(CreatedDeskUser);
        await world.DeletePersonAsync(SettingsWorld.NeonCreated);
        var role = await world.SeedRoleAsync("Technician", Permission.ChatAgentStaff);
        world.DeskAnswers(DeskCreates);
        var email = $"{SettingsWorld.Unique()}@spiritfitness.test";

        try
        {
            var response = await world.PostJsonAsync(People, new { name = "Ann Lee", email = email.ToUpperInvariant(), roleIds = new[] { role }, desk = true, crm = false });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var added = await response.Content.ReadFromJsonAsync<AddedPerson>(Cancel);
            Assert.Equal(new AddSteps(StepResult.Done, StepResult.Done, StepResult.Done, StepResult.None, null), added!.Steps);
            Assert.Equal(SettingsWorld.NeonCreated, added.Person!.Id);
            Assert.Equal(email, added.Person.Email);
            Assert.Equal([role], added.Person.Roles.Select(r => r.Id));
            Assert.Equal(LinkState.Ready, added.Person.Desk);
            var neon = Assert.Single(world.NeonWire.Requests);
            Assert.Equal("POST", neon.Method);
        }
        finally
        {
            await world.DeleteDeskLinkAsync(CreatedDeskUser);
        }
    }

    [Fact]
    public async Task AddingAnEmailSomeoneWithRolesHas_InAnyCase_Is409_AndCallsNoNeon()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var email = $"{SettingsWorld.Unique()}@spiritfitness.test";
        await world.GrantAsync(await world.AddPersonAsync("Ann", email), Permission.ChatAgentGuest);

        var response = await world.PostJsonAsync(People, new { name = "Ann", email = email.ToUpperInvariant(), roleIds = Array.Empty<Guid>(), desk = false, crm = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Somebody already has that email.", await DetailAsync(response));
        Assert.Empty(world.NeonWire.Requests);
    }

    [Fact]
    public async Task AddingAnEmailWithNoRoles_ReusesTheSignIn_AndCallsNoNeon()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var email = $"{SettingsWorld.Unique()}@spiritfitness.test";
        var personId = await world.AddPersonAsync("Ann", email);
        var role = await world.SeedRoleAsync("Guest", Permission.ChatAgentGuest);

        var added = await (await world.PostJsonAsync(People, new { name = "Ann", email, roleIds = new[] { role }, desk = false, crm = false }))
            .Content.ReadFromJsonAsync<AddedPerson>(Cancel);

        Assert.Equal(personId, added!.Person!.Id);
        Assert.Equal((StepResult.Done, StepResult.Done), (added.Steps.Neon, added.Steps.Roles));
        Assert.Empty(world.NeonWire.Requests);
    }

    [Fact]
    public async Task AddingTheEmailOfABannedPersonWithNoRoles_Is409_AndChangesNothing()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var email = $"{SettingsWorld.Unique()}@spiritfitness.test";
        var personId = await world.AddPersonAsync("Ann", email);
        await world.BanAsync(personId);
        var role = await world.SeedRoleAsync("Technician", Permission.ChatAgentStaff);

        var response = await world.PostJsonAsync(People, new { name = "Ann", email, roleIds = new[] { role }, desk = true, crm = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("This person is banned. Unban them first.", await DetailAsync(response));
        var row = await world.RowAsync(personId);
        Assert.Equal((true, 0, LinkState.None, LinkState.None), (row.Banned, row.Roles.Count, row.Desk, row.Crm));
        Assert.Empty(world.NeonWire.Requests);
        Assert.Empty(world.DeskWire.Requests);
        Assert.Empty(world.CrmWire.Requests);
    }

    [Fact]
    public async Task Add_WithNoRoleIdsInTheBody_Is400()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.PostJsonAsync(People, new { name = "Ann", email = $"{SettingsWorld.Unique()}@x.test", desk = false, crm = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(world.NeonWire.Requests);
    }

    [Fact]
    public async Task Add_WhenNeonSaysTheEmailExists_Is409()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        world.NeonAnswers(_ => ("neon_user_exists", HttpStatusCode.BadRequest));

        var response = await world.PostJsonAsync(People, new { name = "Ann", email = $"{SettingsWorld.Unique()}@x.test", roleIds = Array.Empty<Guid>(), desk = false, crm = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Somebody already has that email.", await DetailAsync(response));
    }

    [Fact]
    public async Task Add_WhenNeonIsDown_MakesNothing_AndSaysSo()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        world.NeonAnswers(_ => (null, HttpStatusCode.BadGateway));

        var added = await (await world.PostJsonAsync(People, new { name = "Ann", email = $"{SettingsWorld.Unique()}@x.test", roleIds = Array.Empty<Guid>(), desk = true, crm = true }))
            .Content.ReadFromJsonAsync<AddedPerson>(Cancel);

        Assert.Null(added!.Person);
        Assert.Equal(new AddSteps(StepResult.Failed, StepResult.None, StepResult.None, StepResult.None, "Neon did not answer."), added.Steps);
        Assert.Empty(world.DeskWire.Requests);
    }

    [Fact]
    public async Task Add_WhenNeonIsNotSetUp_Is200_WithTheNeonStepFailed()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, neonSetUp: false);

        var response = await world.PostJsonAsync(People, new { name = "Ann", email = $"{SettingsWorld.Unique()}@x.test", roleIds = Array.Empty<Guid>(), desk = false, crm = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var added = await response.Content.ReadFromJsonAsync<AddedPerson>(Cancel);
        Assert.Equal(new AddedPerson(null, new AddSteps(StepResult.Failed, StepResult.None, StepResult.None, StepResult.None, "Neon is not set up yet.")), added);
        Assert.Empty(world.NeonWire.Requests);
    }

    [Fact]
    public async Task Add_WithARoleThatDoesNotExist_Is400_AndCallsNoNeon()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.PostJsonAsync(People, new { name = "Ann", email = $"{SettingsWorld.Unique()}@x.test", roleIds = new[] { Guid.NewGuid() }, desk = false, crm = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(world.NeonWire.Requests);
    }

    [Fact]
    public async Task SetRoles_ReplacesThem_AndAnswersTheRow()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.GrantAsync(personId, Permission.LookupUnits);
        var tech = await world.SeedRoleAsync("Technician", Permission.ChatAgentStaff);

        var row = await (await world.PutAsync($"{People}/{personId}/roles", new { roleIds = new[] { tech } })).Content.ReadFromJsonAsync<PersonRow>(Cancel);

        Assert.Equal([tech], row!.Roles.Select(role => role.Id));
        Assert.Equal(Permission.ChatAgentStaff, row.Agent);
    }

    [Theory]
    [InlineData("last admin", HttpStatusCode.Conflict, "This would leave Spirit with no admin.")]
    [InlineData("own admin", HttpStatusCode.Forbidden, "You cannot remove your own admin access.")]
    [InlineData("beyond", HttpStatusCode.Forbidden, "You can only give access you have yourself.")]
    public async Task SetRoles_Refusals_UseTheHouseWords(string what, HttpStatusCode status, string words)
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: what == "beyond" ? [Permission.SettingsPeople] : what == "last admin" ? SettingsWorld.Everything : null);
        Guid target;
        object body = new { roleIds = Array.Empty<Guid>() };

        switch (what)
        {
            case "last admin":
                await world.RemoveEveryAdminAsync();
                target = await world.AddPersonAsync();
                await world.MakeAdminAsync(target);
                break;
            case "own admin":
                await world.MakeAdminAsync(await world.AddPersonAsync());
                target = world.CallerId;
                break;
            default:
                target = await world.AddPersonAsync();
                body = new { roleIds = new[] { await world.SeedRoleAsync("Lookup", Permission.LookupUnits) } };
                break;
        }

        var response = await world.PutAsync($"{People}/{target}/roles", body);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(words, await DetailAsync(response));
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("unban")]
    public async Task DeletingOrUnbanningSomeoneWithAccessYouLack_Is403_WithTheHouseWords(string what)
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsPeople]);
        var personId = await world.AddPersonAsync();
        await world.GrantAsync(personId, Permission.LookupUnits);
        await world.BanAsync(personId);

        var response = what == "delete"
            ? await world.DeleteAsync($"{People}/{personId}")
            : await world.PostAsync($"{People}/{personId}/unban");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("You can only give access you have yourself.", await DetailAsync(response));
        var row = await world.RowAsync(personId);
        Assert.Equal((true, 1), (row.Banned, row.Roles.Count));
        Assert.Empty(world.NeonWire.Requests);
    }

    [Fact]
    public async Task Ban_ThenUnban_AreOneCallEach_AndWithNoDeskLinkHaveNoDeskStep()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();

        var banned = await (await world.PostJsonAsync($"{People}/{personId}/ban", new { reason = "Left" })).Content.ReadFromJsonAsync<BanAnswer>(Cancel);
        var unbanned = await (await world.PostAsync($"{People}/{personId}/unban")).Content.ReadFromJsonAsync<BanAnswer>(Cancel);

        Assert.Equal((true, StepResult.None, (string?)null), (banned!.Person.Banned, banned.Desk, banned.Detail));
        Assert.Equal((false, StepResult.None, (string?)null), (unbanned!.Person.Banned, unbanned.Desk, unbanned.Detail));
        Assert.Empty(world.NeonWire.Requests);
        Assert.Empty(world.DeskWire.Requests);
    }

    [Fact]
    public async Task BanningYourself_Is403()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.PostAsync($"{People}/{world.CallerId}/ban");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("You cannot do this to your own account.", await DetailAsync(response));
    }

    [Fact]
    public async Task LinkingDesk_ForSomeoneWithNoRoles_MakesTheLinkReady()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        await world.DeleteDeskLinkAsync(CreatedDeskUser);
        var personId = await world.AddPersonAsync();
        world.DeskAnswers(DeskCreates);

        try
        {
            var response = await world.PostAsync($"{People}/{personId}/desk");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(LinkState.Ready, (await response.Content.ReadFromJsonAsync<PersonRow>(Cancel))!.Desk);
        }
        finally
        {
            await world.DeleteDeskLinkAsync(CreatedDeskUser);
        }
    }

    [Fact]
    public async Task LinkingSomeoneWhoDoesNotExist_Is404()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.PostAsync($"{People}/{Guid.NewGuid()}/desk");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static (string?, HttpStatusCode) DeskCreates(HttpRequestMessage request)
        => (request.Method.Method, request.RequestUri!.AbsolutePath) switch
        {
            ("POST", "/platform/api/v1/users") => ("platform_user_created", HttpStatusCode.OK),
            ("GET", _) => ("agents", HttpStatusCode.OK),
            ("POST", "/platform/api/v1/accounts/2/account_users") => ("account_user_created", HttpStatusCode.OK),
            _ => ("inbox_members", HttpStatusCode.OK),
        };

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        return problem.RootElement.GetProperty("detail").GetString();
    }
}
