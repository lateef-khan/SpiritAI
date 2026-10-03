using System.Net;
using System.Net.Http.Json;

using SpiritAI.Access;
using SpiritAI.Hub;
using SpiritAI.Settings;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Settings;

/// <summary>Deleting a person and unlinking one app: each step is its own, and a retry repeats only what failed.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PersonRemovalTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Delete_LeavesDesk_DeletesCrm_ThenNeon_AndThePersonIsGone()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);
        await world.SeedLinkAsync(personId, HubApps.Crm, SettingsWorld.Unique(), ready: true);
        world.NeonAnswers(_ => (null, HttpStatusCode.NoContent));

        var result = await (await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}")).Content.ReadFromJsonAsync<PersonDeletion>(Cancel);

        Assert.Equal(new PersonDeletion(StepResult.Done, StepResult.Done, StepResult.Done, null), result);
        var deskCall = Assert.Single(world.DeskWire.Requests);
        Assert.Equal(("DELETE", "http://chatwoot.test/platform/api/v1/accounts/2/account_users"), (deskCall.Method, deskCall.Url));
        Assert.Equal(("DELETE", $"https://console.neon.tech/api/v2/projects/proj-1/branches/br-1/auth/users/{personId}"), Assert.Single(world.NeonWire.Requests));
        Assert.DoesNotContain(await world.PeopleAsync(), person => person.Id == personId);
    }

    [Fact]
    public async Task Delete_WhenCrmIsDown_KeepsTheSignIn_AndARetryDoesOnlyWhatIsLeft()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);
        await world.SeedLinkAsync(personId, HubApps.Crm, SettingsWorld.Unique(), ready: true);
        world.CrmAnswers(HttpStatusCode.BadGateway);
        world.NeonAnswers(_ => (null, HttpStatusCode.NoContent));

        var first = await (await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}")).Content.ReadFromJsonAsync<PersonDeletion>(Cancel);
        Assert.Equal((StepResult.Done, StepResult.Failed, StepResult.Failed), (first!.Desk, first.Crm, first.Neon));
        Assert.Empty(world.NeonWire.Requests);
        Assert.Equal(LinkState.Ready, (await world.RowAsync(personId)).Crm);

        world.CrmAnswers(HttpStatusCode.NoContent);
        var deskCalls = world.DeskWire.Requests.Count;
        var second = await (await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}")).Content.ReadFromJsonAsync<PersonDeletion>(Cancel);

        Assert.Equal(new PersonDeletion(StepResult.None, StepResult.Done, StepResult.Done, null), second);
        Assert.Equal(deskCalls, world.DeskWire.Requests.Count);
    }

    [Fact]
    public async Task Delete_WhenDeskIsDown_KeepsTheDeskLink_StillTriesCrm_AndKeepsTheSignIn()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);
        await world.SeedLinkAsync(personId, HubApps.Crm, SettingsWorld.Unique(), ready: true);
        world.DeskAnswers(_ => throw new TaskCanceledException("timed out"));

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PersonDeletion>(Cancel);
        Assert.Equal((StepResult.Failed, StepResult.Done, StepResult.Failed), (result!.Desk, result.Crm, result.Neon));
        Assert.Single(world.CrmWire.Requests);
        Assert.Empty(world.NeonWire.Requests);
        Assert.Equal(LinkState.Ready, (await world.RowAsync(personId)).Desk);
    }

    [Fact]
    public async Task Delete_WhenNeonIsNotSetUp_KeepsTheSignIn_AndSaysSo()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, neonSetUp: false);
        var personId = await world.AddPersonAsync();

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new PersonDeletion(StepResult.None, StepResult.None, StepResult.Failed, "Neon is not set up yet."),
            await response.Content.ReadFromJsonAsync<PersonDeletion>(Cancel));
        Assert.Contains(await world.PeopleAsync(), person => person.Id == personId);
    }

    [Fact]
    public async Task DeletingTheLastAdmin_Is409_AndCallsNoApp()
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: SettingsWorld.Everything);
        await world.RemoveEveryAdminAsync();
        var admin = await world.AddPersonAsync();
        await world.MakeAdminAsync(admin);
        await world.SeedLinkAsync(admin, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{admin}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using (var problem = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel)))
        {
            Assert.Equal("This would leave Spirit with no admin.", problem.RootElement.GetProperty("detail").GetString());
        }

        Assert.Empty(world.DeskWire.Requests);
        Assert.Empty(world.NeonWire.Requests);
        Assert.Equal([AdminRole.Id], (await world.RowAsync(admin)).Roles.Select(role => role.Id));
    }

    [Fact]
    public async Task DeletingYourself_Is403()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{world.CallerId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(world.NeonWire.Requests);
    }

    [Fact]
    public async Task UnlinkingYourOwnDesk_Is403()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{world.CallerId}/desk");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("You cannot do this to your own account.", await SettingsWorld.DetailAsync(response));
    }

    [Theory]
    [InlineData("desk")]
    [InlineData("crm")]
    public async Task UnlinkingSomeoneWhoseRolesHoldMoreThanYou_Is403_AndCallsNoApp(string app)
    {
        await using var world = await SettingsWorld.StartAsync(fixture, callerHolds: [Permission.SettingsPeople]);
        var personId = await world.AddPersonAsync();
        await world.MakeAdminAsync(personId);
        await world.SeedLinkAsync(personId, app, app == HubApps.Desk ? SettingsWorld.DeskUserId() : SettingsWorld.Unique(), ready: true);

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}/{app}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("You can only give access you have yourself.", await SettingsWorld.DetailAsync(response));
        Assert.Empty(world.DeskWire.Requests);
        Assert.Empty(world.CrmWire.Requests);
        var row = await world.RowAsync(personId);
        Assert.Equal(LinkState.Ready, app == HubApps.Desk ? row.Desk : row.Crm);
    }

    [Fact]
    public async Task UnlinkCrm_OnTheLastTwentyAdmin_Is409_AndKeepsTheLink()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Crm, SettingsWorld.Unique(), ready: true);
        world.CrmAnswers(HttpStatusCode.Conflict);

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}/crm");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(LinkState.Ready, (await world.RowAsync(personId)).Crm);
    }

    [Fact]
    public async Task UnlinkDesk_LeavesTheAccount_AndDropsTheLink()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        var deskId = SettingsWorld.DeskUserId();
        await world.SeedLinkAsync(personId, HubApps.Desk, deskId, ready: true);

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}/desk");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DELETE", Assert.Single(world.DeskWire.Requests).Method);
        Assert.Equal(LinkState.None, (await world.RowAsync(personId)).Desk);
    }

    [Fact]
    public async Task Unlink_WithNoLink_Is404()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();

        var response = await world.DeleteAsync($"{SettingsEndpoints.Pattern}/people/{personId}/desk");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
