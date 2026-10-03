using System.Data.Common;
using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.Access;
using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.Hub;
using SpiritAI.Settings;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Settings;

/// <summary>
/// Ban and Unban reach Desk in the same request (access spec section 11a); role changes never do.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BanDeskTests(PostgresFixture fixture)
{
    private const string People = $"{SettingsEndpoints.Pattern}/people";

    private const string AccountUsers = "http://chatwoot.test/platform/api/v1/accounts/2/account_users";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ABan_LeavesTheAccount_AndTheLinkIsNotReady()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);

        var answer = await BanAsync(world, personId);

        Assert.Equal((true, LinkState.Unfinished, StepResult.Done, (string?)null), (answer.Person.Banned, answer.Person.Desk, answer.Desk, answer.Detail));
        Assert.Equal(("DELETE", AccountUsers), Assert.Single(world.DeskWire.Requests));
    }

    [Fact]
    public async Task ABanChatwootRefuses_Stands_SaysTheDeskStepFailed_AndBanAgainFinishesIt()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);
        world.DeskAnswers(_ => (null, HttpStatusCode.BadGateway));

        var failed = await BanAsync(world, personId);

        Assert.Equal((true, LinkState.Ready, StepResult.Failed), (failed.Person.Banned, failed.Person.Desk, failed.Desk));
        Assert.Equal("Desk did not answer. Press Ban again to finish.", failed.Detail);

        world.DeskAnswers(_ => (null, HttpStatusCode.OK));
        var again = await BanAsync(world, personId);

        Assert.Equal((true, LinkState.Unfinished, StepResult.Done), (again.Person.Banned, again.Person.Desk, again.Desk));
    }

    [Fact]
    public async Task AnUnban_RejoinsTheAccountAndInbox_AndTheLinkIsReady()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: false);
        await world.BanAsync(personId);
        world.DeskAnswers(Joins);

        var answer = await UnbanAsync(world, personId);

        Assert.Equal((false, LinkState.Ready, StepResult.Done), (answer.Person.Banned, answer.Person.Desk, answer.Desk));
        Assert.Equal(["GET", "POST", "POST"], world.DeskWire.Requests.Select(request => request.Method));
    }

    [Fact]
    public async Task AnUnbanChatwootRefuses_Stands_SaysTheDeskStepFailed_AndUnbanAgainFinishesIt()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: false);
        await world.BanAsync(personId);
        world.DeskAnswers(_ => (null, HttpStatusCode.BadGateway));

        var failed = await UnbanAsync(world, personId);

        Assert.Equal((false, LinkState.Unfinished, StepResult.Failed), (failed.Person.Banned, failed.Person.Desk, failed.Desk));
        Assert.Equal("Desk did not answer. Press Unban again to finish.", failed.Detail);

        world.DeskAnswers(Joins);
        var again = await UnbanAsync(world, personId);

        Assert.Equal((LinkState.Ready, StepResult.Done), (again.Person.Desk, again.Desk));
    }

    [Fact]
    public async Task APersonDeletedWhileTheUnbanJoins_LeavesTheAccountAgain()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: false);
        world.DeskAnswers(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                using var db = fixture.Open();
                db.Database.ExecuteSql($"""DELETE FROM neon_auth."user" WHERE id = {personId}""");
            }

            return Joins(request);
        });

        var response = await world.PostAsync($"{People}/{personId}/unban");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["GET", "POST", "POST", "DELETE"], world.DeskWire.Requests.Select(request => request.Method));
    }

    [Fact]
    public async Task AnUnbanWhoseChatwootAnswerIsNotJson_Stands_AndSaysTheDeskStepFailed()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: false);
        await world.BanAsync(personId);
        world.DeskAnswers(_ => ("not_json", HttpStatusCode.OK));

        var answer = await UnbanAsync(world, personId);

        Assert.Equal((false, LinkState.Unfinished, StepResult.Failed), (answer.Person.Banned, answer.Person.Desk, answer.Desk));
        Assert.Equal("Desk did not answer. Press Unban again to finish.", answer.Detail);
    }

    [Fact]
    public async Task AnUnbanWhoseLinkIsGoneWhenTheJoinReadsIt_MakesNoDeskUserAndNoLink()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        var deskUserId = SettingsWorld.DeskUserId();
        await world.SeedLinkAsync(personId, HubApps.Desk, deskUserId, ready: false);
        await using var scope = world.Scope();
        await using var db = OpenWith(new DropLinkAfterRead(fixture, deskUserId));
        var banDesk = new BanDesk(db, scope.ServiceProvider.GetRequiredService<DeskUsers>(), scope.ServiceProvider.GetRequiredService<LinkPerson>(), NullLogger<BanDesk>.Instance);

        var (result, detail) = await banDesk.RejoinAsync(personId, Cancel);

        Assert.Equal((StepResult.None, (string?)null), (result, detail));
        Assert.Empty(world.DeskWire.Requests);
        await using var check = fixture.Open();
        Assert.False(await check.LinkedUsers.AnyAsync(link => link.UserId == personId, Cancel));
    }

    [Fact]
    public async Task ABanCancelledRightAfterChatwootLetTheUserGo_StillMarksTheLinkNotReady()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        await using var scope = world.Scope();
        using var http = new HttpClient(new CancelWhenAnswerIsDone(request)) { BaseAddress = new Uri("http://chatwoot.test") };
        var deskUsers = new DeskUsers(http, scope.ServiceProvider.GetRequiredService<IOptions<ChatwootOptions>>());
        await using var db = fixture.Open();
        var banDesk = new BanDesk(db, deskUsers, scope.ServiceProvider.GetRequiredService<LinkPerson>(), NullLogger<BanDesk>.Instance);

        var (result, _) = await banDesk.LeaveAsync(personId, request.Token);

        Assert.Equal(StepResult.Done, result);
        Assert.Equal(LinkState.Unfinished, (await world.RowAsync(personId)).Desk);
    }

    [Fact]
    public async Task RoleChanges_NeverCallDesk_AndKeepTheLinkReady()
    {
        await using var world = await SettingsWorld.StartAsync(fixture);
        var personId = await world.AddPersonAsync();
        var roleId = await world.GrantAsync(personId, Permission.ChatAgentStaff);
        await world.SeedLinkAsync(personId, HubApps.Desk, SettingsWorld.DeskUserId(), ready: true);
        var other = await world.SeedRoleAsync("Guest", Permission.ChatAgentGuest);

        Assert.Equal(HttpStatusCode.OK, (await world.PutAsync($"{People}/{personId}/roles", new { roleIds = new[] { roleId, other } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.PutAsync($"{SettingsEndpoints.Pattern}/roles/{roleId}", new { name = $"Edited {SettingsWorld.Unique()}", description = (string?)null, permissions = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.DeleteAsync($"{SettingsEndpoints.Pattern}/roles/{roleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.PutAsync($"{People}/{personId}/roles", new { roleIds = Array.Empty<Guid>() })).StatusCode);

        Assert.Empty(world.DeskWire.Requests);
        Assert.Empty(world.CrmWire.Requests);
        Assert.Equal(LinkState.Ready, (await world.RowAsync(personId)).Desk);
    }

    private SpiritDbContext OpenWith(IInterceptor interceptor)
    {
        using var probe = fixture.Open();
        var options = new DbContextOptionsBuilder<SpiritDbContext>().AddInterceptors(interceptor);
        options.UseSpiritNpgsql(probe.Database.GetConnectionString()!);
        return new SpiritDbContext(options.Options);
    }

    private static (string?, HttpStatusCode) Joins(HttpRequestMessage request)
        => request.Method == HttpMethod.Get ? ("agents", HttpStatusCode.OK) : ("inbox_members", HttpStatusCode.OK);

    private static async Task<BanAnswer> BanAsync(SettingsWorld world, Guid personId)
    {
        var response = await world.PostJsonAsync($"{People}/{personId}/ban", new { reason = "Left" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BanAnswer>(Cancel))!;
    }

    private static async Task<BanAnswer> UnbanAsync(SettingsWorld world, Guid personId)
    {
        var response = await world.PostAsync($"{People}/{personId}/unban");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BanAnswer>(Cancel))!;
    }

    /// <summary>Plays an admin whose Unlink lands right after the Unban has read the link.</summary>
    private sealed class DropLinkAfterRead(PostgresFixture fixture, string deskUserId) : DbCommandInterceptor
    {
        private bool _dropped;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!_dropped && command.CommandText.Contains("linked_user", StringComparison.Ordinal))
            {
                _dropped = true;
                await using var other = fixture.Open();
                await other.LinkedUsers.Where(link => link.ExternalId == deskUserId).ExecuteDeleteAsync(cancellationToken);
            }

            return result;
        }
    }

    /// <summary>
    /// Answers 200, and plays a caller who gives up once Chatwoot's answer is read and closed:
    /// the response is disposed right before the Desk call returns.
    /// </summary>
    private sealed class CancelWhenAnswerIsDone(CancellationTokenSource request) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new CancelOnDispose(request) });

        private sealed class CancelOnDispose(CancellationTokenSource request) : ByteArrayContent([])
        {
            protected override void Dispose(bool disposing)
            {
                request.Cancel();
                base.Dispose(disposing);
            }
        }
    }
}
