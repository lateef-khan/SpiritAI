using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AgentCore.Application.Configuration.Parsing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;
using SpiritAI.Auth;
using SpiritAI.Chatwoot;
using SpiritAI.Hub;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;
using SpiritAI.Twenty;

using Xunit;

namespace SpiritAI.Tests.Hub;

/// <summary>
/// Settings' routes through a real host: only <see cref="AccessPolicies.Admin"/> opens them (hub
/// spec, section 4.5; global constraints, Review Focus #5).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SettingsEndpointsTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ANonAdmin_Is403_OnBothRoutes()
    {
        await using var world = await World.StartAsync(fixture, [AccessGroup.TechServiceManager]);

        var list = await world.GetAsync($"{SettingsEndpoints.Pattern}/people");
        var link = await world.PostAsync($"{SettingsEndpoints.Pattern}/people/{Guid.NewGuid()}/desk");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, link.StatusCode);
    }

    [Fact]
    public async Task TheList_ShowsEveryPersonWithGroupsAndLinkStates()
    {
        var deskReady = await AddPersonAsync("Desk Ready", Unique() + "@spiritfitness.test");
        await GrantAsync(deskReady, AccessGroup.TechService);
        await SeedLinkAsync(deskReady, HubApps.Desk, Unique(), ready: true);

        var crmUnfinished = await AddPersonAsync("Crm Unfinished", Unique() + "@spiritfitness.test");
        await GrantAsync(crmUnfinished, AccessGroup.InsideSales);
        await SeedLinkAsync(crmUnfinished, HubApps.Crm, Unique(), ready: false);

        await using var world = await World.StartAsync(fixture, [AccessGroup.Admin]);

        var raw = await (await world.GetAsync($"{SettingsEndpoints.Pattern}/people")).Content.ReadAsStringAsync(Cancel);
        using var wire = JsonDocument.Parse(raw);
        var wireGroup = wire.RootElement.EnumerateArray()
            .Single(person => person.GetProperty("id").GetGuid() == deskReady)
            .GetProperty("groups")[0];
        Assert.Equal(JsonValueKind.String, wireGroup.ValueKind);
        Assert.Equal("TechService", wireGroup.GetString());

        var people = await world.PeopleAsync();

        var first = people.Single(person => person.Id == deskReady);
        Assert.Equal([AccessGroup.TechService], first.Groups);
        Assert.Equal(LinkState.Ready, first.Desk);
        Assert.Equal(LinkState.None, first.Crm);

        var second = people.Single(person => person.Id == crmUnfinished);
        Assert.Equal([AccessGroup.InsideSales], second.Groups);
        Assert.Equal(LinkState.None, second.Desk);
        Assert.Equal(LinkState.Unfinished, second.Crm);
    }

    [Fact]
    public async Task AnEmptyTwentyBaseUrl_Is503_WithTheHouseWords()
    {
        var personId = await AddPersonAsync("No Crm Yet", Unique() + "@spiritfitness.test");

        try
        {
            await using var world = await World.StartAsync(fixture, [AccessGroup.Admin], twentyBaseUrl: string.Empty);

            var response = await world.PostAsync($"{SettingsEndpoints.Pattern}/people/{personId}/crm");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
            Assert.Equal("CRM is not set up yet.", problem.RootElement.GetProperty("detail").GetString());
        }
        finally
        {
            await DeletePersonAsync(personId);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Unique() => Guid.NewGuid().ToString("N");

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

    private async Task GrantAsync(Guid personId, AccessGroup group)
    {
        await using var db = fixture.Open();
        var role = group.ToString();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO spirit.role (name, access_group) VALUES ({role}, {role}) ON CONFLICT (name) DO NOTHING""",
            Cancel);
        db.UserRoles.Add(new UserRole { UserId = personId, Role = role });
        await db.SaveChangesAsync(Cancel);
    }

    private async Task SeedLinkAsync(Guid personId, string app, string externalId, bool ready)
    {
        await using var db = fixture.Open();
        db.LinkedUsers.Add(new LinkedUser { UserId = personId, App = app, ExternalId = externalId, Ready = ready });
        await db.SaveChangesAsync(Cancel);
    }

    /// <summary>A document that declares every entry a group runs, so the start-up check passes.</summary>
    private const string Document = """
        apiVersion: agentcore/v1
        agents:
          items:
            - { id: spirit, instructions: "answer" }
        entries:
          main: { agent: spirit }
          dealer: { agent: spirit }
          staff: { agent: spirit }
          manager: { agent: spirit }
          admin: { agent: spirit }
        """;

    private sealed class World(WebApplication app, string token, ReplayingHandler deskWire) : IAsyncDisposable
    {
        private readonly HttpClient _client = app.GetTestClient();

        public ReplayingHandler DeskWire { get; } = deskWire;

        public static async Task<World> StartAsync(
            PostgresFixture fixture,
            AccessGroup[] groups,
            ReplayingHandler? deskWire = null,
            string twentyBaseUrl = "http://twenty.test",
            ReplayingHandler? crmWire = null)
        {
            var kit = new NeonAuthTestKit();
            deskWire ??= new ReplayingHandler(payload: null) { Folder = "Hub" };
            crmWire ??= new ReplayingHandler(payload: null) { Folder = "Hub" };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{NeonAuthOptions.SectionName}:BaseUrl"] = NeonAuthTestKit.BaseUrl,
                    [$"{HubOptions.SectionName}:DeskUrl"] = "http://desk.spirit.test",
                    [$"{HubOptions.SectionName}:CrmUrl"] = "http://crm.spirit.test",
                    [$"{TwentyOptions.SectionName}:BaseUrl"] = twentyBaseUrl,
                    [$"{TwentyOptions.SectionName}:HubSecret"] = "hub-secret",
                    [$"{ChatwootOptions.SectionName}:BaseUrl"] = "http://chatwoot.test",
                    [$"{ChatwootOptions.SectionName}:AccountId"] = "2",
                    [$"{ChatwootOptions.SectionName}:InboxId"] = "1",
                    [$"{ChatwootOptions.SectionName}:PlatformToken"] = "platform-token",
                    [$"{ChatwootOptions.SectionName}:AdminToken"] = "admin-token",
                })
                .Build();

            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddSingleton(TestHybridCache.Create());
            builder.Services.AddSingleton(ConfigurationLoader.LoadYaml(Document));
            builder.Services.AddNeonAuth(configuration);
            builder.Services.AddSingleton(kit.Validator());
            builder.Services.AddAccess();
            builder.Services.AddScoped<IUserAccess>(_ => new FixedAccess(groups));

            builder.Services.Configure<ChatwootOptions>(configuration.GetSection(ChatwootOptions.SectionName));
            builder.Services.AddScoped(_ => fixture.Open());
            builder.Services.AddHub(configuration);
            builder.Services.AddHttpClient<DeskUsers>().ConfigurePrimaryHttpMessageHandler(() => deskWire);
            builder.Services.AddHttpClient<CrmUsers>().ConfigurePrimaryHttpMessageHandler(() => crmWire);

            var app = builder.Build();

            app.UseNeonAuthOnApi();
            app.UseAuthorization();

            app.MapHub();
            app.MapSettings();

            await app.StartAsync(TestContext.Current.CancellationToken);

            return new World(app, kit.Token(subject: Guid.NewGuid().ToString()), deskWire);
        }

        public async Task<IReadOnlyList<PersonRow>> PeopleAsync()
        {
            var response = await GetAsync($"{SettingsEndpoints.Pattern}/people");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PersonRow>>(TestContext.Current.CancellationToken))!;
        }

        public Task<HttpResponseMessage> GetAsync(string route)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public Task<HttpResponseMessage> PostAsync(string route)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, route);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class FixedAccess(AccessGroup[] groups) : IUserAccess
    {
        public ValueTask<IReadOnlyList<AccessGroup>> GroupsOfAsync(Guid userId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<AccessGroup>>(groups);
    }
}
