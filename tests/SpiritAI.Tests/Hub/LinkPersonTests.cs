using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Hub;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;
using SpiritAI.Twenty;

using Xunit;

namespace SpiritAI.Tests.Hub;

/// <summary>
/// The Desk and CRM create steps against real Chatwoot and Twenty replies (hub spec, sections 5.3
/// and 6.3), and the retry and race cases Review Focus #1 calls out.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class LinkPersonTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AFreshDeskCreate_MakesTheUser_JoinsAccountAndInbox_AndEndsReady()
    {
        // platform_user_created.json always answers id 4: the same Chatwoot id every run, so the
        // row is deleted afterward or a second run collides on (app, external_id).
        var personId = await AddPersonAsync("Lateef Khan", Unique() + "@spiritfitness.test");

        try
        {
            var wire = new ReplayingHandler(["platform_user_created", "agents", "account_user_created", "inbox_members"]) { Folder = "Hub" };
            await using var db = fixture.Open();

            var state = await new LinkPerson(db, DeskOf(wire), CrmOf(new ReplayingHandler(payload: null)))
                .RunAsync(personId, HubApps.Desk, Cancel);

            Assert.Equal(LinkState.Ready, state);
            var link = await LinkAsync(personId, HubApps.Desk);
            Assert.True(link!.Ready);
            Assert.Equal("4", link.ExternalId);
            Assert.Equal(4, wire.Requests.Count);
            Assert.Equal(
                [
                    "http://chatwoot.test/platform/api/v1/users",
                    "http://chatwoot.test/api/v1/accounts/2/agents",
                    "http://chatwoot.test/platform/api/v1/accounts/2/account_users",
                    "http://chatwoot.test/api/v1/accounts/2/inbox_members",
                ],
                wire.Requests.Select(request => request.Url));
        }
        finally
        {
            await DeletePersonAsync(personId);
        }
    }

    [Fact]
    public async Task AnEmailAlreadyInDesk_IsAdopted_AndKeepsItsAdministratorRole()
    {
        // platform_user_adopted.json is the desk-admin user, id 1, which agents.json lists as an
        // administrator. Joining the account again would make it an agent, so that call must not happen.
        var personId = await AddPersonAsync("Desk Admin", "desk-admin@spiritfitness.test");

        try
        {
            var wire = new ReplayingHandler(["platform_user_adopted", "agents", "inbox_members"]) { Folder = "Hub" };
            await using var db = fixture.Open();

            var state = await new LinkPerson(db, DeskOf(wire), CrmOf(new ReplayingHandler(payload: null)))
                .RunAsync(personId, HubApps.Desk, Cancel);

            Assert.Equal(LinkState.Ready, state);
            var link = await LinkAsync(personId, HubApps.Desk);
            Assert.True(link!.Ready);
            Assert.Equal("1", link.ExternalId);
            Assert.Equal(
                [
                    "http://chatwoot.test/platform/api/v1/users",
                    "http://chatwoot.test/api/v1/accounts/2/agents",
                    "http://chatwoot.test/api/v1/accounts/2/inbox_members",
                ],
                wire.Requests.Select(request => request.Url));
        }
        finally
        {
            await DeletePersonAsync(personId);
        }
    }

    [Fact]
    public async Task AHalfDoneCreate_ResumesAtTheAccount_WithoutASecondUser()
    {
        var personId = await AddPersonAsync("Half Done", Unique() + "@spiritfitness.test");
        var externalId = Random.Shared.Next(1, int.MaxValue).ToString();
        await SeedLinkAsync(personId, HubApps.Desk, externalId, ready: false);
        var wire = new ReplayingHandler(["agents", "account_user_created", "inbox_members"]) { Folder = "Hub" };
        await using var db = fixture.Open();

        var state = await new LinkPerson(db, DeskOf(wire), CrmOf(new ReplayingHandler(payload: null)))
            .RunAsync(personId, HubApps.Desk, Cancel);

        Assert.Equal(LinkState.Ready, state);
        var link = await LinkAsync(personId, HubApps.Desk);
        Assert.True(link!.Ready);
        Assert.Equal(externalId, link.ExternalId);
        Assert.Equal(3, wire.Requests.Count);
        Assert.DoesNotContain(wire.Requests, request => request.Url.Contains("/platform/api/v1/users", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AReadyLink_IsLeftAlone()
    {
        var personId = await AddPersonAsync("Already Ready", Unique() + "@spiritfitness.test");
        await SeedLinkAsync(personId, HubApps.Desk, Unique(), ready: true);
        var wire = new ReplayingHandler(payload: null) { Folder = "Hub" };
        await using var db = fixture.Open();

        var state = await new LinkPerson(db, DeskOf(wire), CrmOf(new ReplayingHandler(payload: null)))
            .RunAsync(personId, HubApps.Desk, Cancel);

        Assert.Equal(LinkState.Ready, state);
        Assert.Empty(wire.Requests);
    }

    [Fact]
    public async Task ACrmEmailTwentyAlreadyHas_IsRefused()
    {
        var personId = await AddPersonAsync("Ann Lee", Unique() + "@spiritfitness.test");
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.Conflict) { Folder = "Hub" };
        await using var db = fixture.Open();

        var refused = await Assert.ThrowsAsync<EmailAlreadyUsedException>(
            () => new LinkPerson(db, DeskOf(new ReplayingHandler(payload: null)), CrmOf(wire)).RunAsync(personId, HubApps.Crm, Cancel));

        Assert.Equal("email already used in CRM", refused.Message);
        Assert.Null(await LinkAsync(personId, HubApps.Crm));
    }

    [Fact]
    public async Task ARaceOnTheInsert_IsCaught_AndResumesWithTheWinningRow()
    {
        var personId = await AddPersonAsync("Raced Person", Unique() + "@spiritfitness.test");
        var winningExternalId = Random.Shared.Next(1, int.MaxValue).ToString();
        var wire = new ReplayingHandler(["platform_user_created", "agents", "account_user_created", "inbox_members"]) { Folder = "Hub" };
        var racing = new RivalInsertsOnCreate(wire, fixture, personId, winningExternalId);
        await using var db = fixture.Open();

        var state = await new LinkPerson(db, DeskOf(racing), CrmOf(new ReplayingHandler(payload: null)))
            .RunAsync(personId, HubApps.Desk, Cancel);

        Assert.Equal(LinkState.Ready, state);
        var link = await LinkAsync(personId, HubApps.Desk);
        Assert.True(link!.Ready);
        Assert.Equal(winningExternalId, link.ExternalId);
        Assert.Equal(4, wire.Requests.Count);
        Assert.Equal($$"""{"user_id":{{winningExternalId}},"role":"agent"}""", wire.Requests[2].Body);
        Assert.Equal($$"""{"inbox_id":1,"user_ids":[{{winningExternalId}}]}""", wire.Requests[3].Body);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Unique() => Guid.NewGuid().ToString("N");

    private static DeskUsers DeskOf(HttpMessageHandler wire) => new(
        new HttpClient(wire),
        Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test", AccountId = 2, InboxId = 1,
            PlatformToken = "platform-token", AdminToken = "admin-token",
        }));

    private static CrmUsers CrmOf(HttpMessageHandler wire) => new(
        new HttpClient(wire),
        Options.Create(new TwentyOptions { BaseUrl = "http://twenty.test", HubSecret = "hub-secret" }),
        Options.Create(new HubOptions { CrmUrl = "https://crm.spirit.test" }),
        new TestTimeProvider(DateTimeOffset.UtcNow));

    /// <summary>Adds a Neon user made for this test alone, so tests never share a row.</summary>
    private async Task<Guid> AddPersonAsync(string name, string email)
    {
        var personId = Guid.NewGuid();

        await using var db = fixture.Open();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({personId}, {name}, {email}, true)""",
            Cancel);

        return personId;
    }

    private async Task DeletePersonAsync(Guid personId)
    {
        await using var db = fixture.Open();
        await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {personId}""", Cancel);
    }

    private async Task SeedLinkAsync(Guid personId, string app, string externalId, bool ready)
    {
        await using var db = fixture.Open();
        db.LinkedUsers.Add(new LinkedUser { UserId = personId, App = app, ExternalId = externalId, Ready = ready });
        await db.SaveChangesAsync(Cancel);
    }

    private async Task<LinkedUser?> LinkAsync(Guid personId, string app)
    {
        await using var db = fixture.Open();
        return await db.LinkedUsers.AsNoTracking().SingleOrDefaultAsync(l => l.UserId == personId && l.App == app, Cancel);
    }

    /// <summary>
    /// Simulates a second admin winning the race: right after Chatwoot answers the create, but
    /// before this call's own insert, another row lands under the same primary key.
    /// </summary>
    private sealed class RivalInsertsOnCreate(HttpMessageHandler inner, PostgresFixture fixture, Guid personId, string winningExternalId)
        : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/platform/api/v1/users")
            {
                await using var db = fixture.Open();
                db.LinkedUsers.Add(new LinkedUser { UserId = personId, App = HubApps.Desk, ExternalId = winningExternalId, Ready = false });
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return response;
        }
    }
}
