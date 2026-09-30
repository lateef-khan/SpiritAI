using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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
/// The Hub's tiles and sign-in, through a real Neon token and the same middleware order as
/// <c>Program.cs</c> (hub spec, D4, D5).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class HubEndpointsTests(PostgresFixture fixture)
{
    [Fact]
    public async Task APersonWithNoLinks_SeesOnlyTheChat()
    {
        var userId = await AddPersonAsync();
        await using var world = await World.StartAsync(fixture, userId, AccessGroup.TechService);

        var apps = await world.AppsAsync();

        Assert.Equal(["chat"], apps.Tiles.Select(tile => tile.Id));
    }

    [Fact]
    public async Task APersonWithAReadyDeskLink_SeesDesk_AndAnUnfinishedCrmLinkShowsNothing()
    {
        var userId = await AddPersonAsync();
        await AddLinkAsync(userId, HubApps.Desk, Unique(), ready: true);
        await AddLinkAsync(userId, HubApps.Crm, Unique(), ready: false);
        await using var world = await World.StartAsync(fixture, userId, AccessGroup.TechService);

        var apps = await world.AppsAsync();

        Assert.Equal(["chat", "desk"], apps.Tiles.Select(tile => tile.Id));
        Assert.Equal("http://desk.spirit.test/app", apps.Tiles.Single(tile => tile.Id == "desk").Url);
    }

    [Fact]
    public async Task ATrailingSlashOnDeskOrCrmUrl_DoesNotDoubleUpInTheTileUrl()
    {
        var userId = await AddPersonAsync();
        await AddLinkAsync(userId, HubApps.Desk, Unique(), ready: true);
        await AddLinkAsync(userId, HubApps.Crm, Unique(), ready: true);
        await using var world = await World.StartAsync(
            fixture, userId, "http://desk.spirit.test/", "http://crm.spirit.test/", AccessGroup.TechService);

        var apps = await world.AppsAsync();

        Assert.Equal("http://desk.spirit.test/app", apps.Tiles.Single(tile => tile.Id == "desk").Url);
        Assert.Equal("http://crm.spirit.test/", apps.Tiles.Single(tile => tile.Id == "crm").Url);
    }

    [Fact]
    public async Task AnAdmin_SeesSettings()
    {
        var userId = await AddPersonAsync();
        await using var world = await World.StartAsync(fixture, userId, AccessGroup.Admin);

        var apps = await world.AppsAsync();

        Assert.Contains("settings", apps.Tiles.Select(tile => tile.Id));
        Assert.Equal("/chat/settings.html", apps.Tiles.Single(tile => tile.Id == "settings").Url);
    }

    [Fact]
    public async Task TheDeskSignIn_ReturnsChatwootsLink_ForTheCallersOwnUser()
    {
        var userId = await AddPersonAsync();
        var externalId = Random.Shared.Next(1, int.MaxValue);
        await AddLinkAsync(userId, HubApps.Desk, externalId.ToString(), ready: true);
        await using var world = await World.StartAsync(fixture, userId, AccessGroup.TechService);

        var response = await world.SignInAsync(HubApps.Desk);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HubSignIn>(TestContext.Current.CancellationToken);
        Assert.Equal(
            "http://desk.spirit.localhost:53000/app/login?email=lateef.khan%40spiritfitness.test&sso_auth_token=REDACTED",
            body!.Url);
        var request = Assert.Single(world.DeskWire.Requests);
        Assert.Equal($"http://chatwoot.test/platform/api/v1/users/{externalId}/login", request.Url);
    }

    [Fact]
    public async Task ASignInWithNoReadyLink_Is404_AndCallsNoApp()
    {
        var userId = await AddPersonAsync();
        await using var world = await World.StartAsync(fixture, userId, AccessGroup.TechService);

        var response = await world.SignInAsync(HubApps.Crm);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(world.CrmWire.Requests);
    }

    [Fact]
    public async Task ADeskLinkBelongingToAnotherPerson_Is404_AndCallsNoApp()
    {
        var owner = await AddPersonAsync();
        await AddLinkAsync(owner, HubApps.Desk, Unique(), ready: true);
        var userId = await AddPersonAsync();
        await using var world = await World.StartAsync(fixture, userId, AccessGroup.TechService);

        var response = await world.SignInAsync(HubApps.Desk);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(world.DeskWire.Requests);
    }

    [Fact]
    public async Task AnUnknownApp_Is404()
    {
        var userId = await AddPersonAsync();
        await using var world = await World.StartAsync(fixture, userId, AccessGroup.TechService);

        var response = await world.SignInAsync("mail");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task APersonWithNoAccessGroup_SeesNoTiles()
    {
        var userId = await AddPersonAsync();
        await using var world = await World.StartAsync(fixture, userId);

        var apps = await world.AppsAsync();

        Assert.Empty(apps.Tiles);
    }

    [Fact]
    public async Task APersonWithAReadyLinkButNoAccessGroup_Is404_AndCallsNoApp()
    {
        var userId = await AddPersonAsync();
        await AddLinkAsync(userId, HubApps.Desk, Unique(), ready: true);
        await using var world = await World.StartAsync(fixture, userId);

        var response = await world.SignInAsync(HubApps.Desk);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(world.DeskWire.Requests);
    }

    /// <summary>Adds a Neon user made for this test alone, so tests never share a row.</summary>
    private async Task<Guid> AddPersonAsync()
    {
        var userId = Guid.NewGuid();

        await using var db = fixture.Open();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({userId}, 'Test', {userId.ToString("N") + "@local.test"}, true)""",
            TestContext.Current.CancellationToken);

        return userId;
    }

    private async Task AddLinkAsync(Guid userId, string app, string externalId, bool ready)
    {
        await using var db = fixture.Open();
        db.LinkedUsers.Add(new LinkedUser { UserId = userId, App = app, ExternalId = externalId, Ready = ready });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static string Unique() => Guid.NewGuid().ToString("N");

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

    private sealed class World(WebApplication app, string token, ReplayingHandler deskWire, ReplayingHandler crmWire)
        : IAsyncDisposable
    {
        private readonly HttpClient _client = app.GetTestClient();

        public ReplayingHandler DeskWire { get; } = deskWire;

        public ReplayingHandler CrmWire { get; } = crmWire;

        public static Task<World> StartAsync(PostgresFixture fixture, Guid userId, params AccessGroup[] groups)
            => StartAsync(fixture, userId, "http://desk.spirit.test", "http://crm.spirit.test", groups);

        /// <summary>Starts a Hub whose <see cref="HubOptions.DeskUrl"/> and <see cref="HubOptions.CrmUrl"/>
        /// are exactly <paramref name="deskUrl"/> and <paramref name="crmUrl"/>, unmodified — so a test can
        /// carry a trailing <c>/</c> through to the tile URL it asserts on.</summary>
        public static async Task<World> StartAsync(
            PostgresFixture fixture, Guid userId, string deskUrl, string crmUrl, params AccessGroup[] groups)
        {
            var kit = new NeonAuthTestKit();
            var deskWire = new ReplayingHandler("sso_link") { Folder = "Hub" };
            var crmWire = new ReplayingHandler(payload: null) { Folder = "Hub" };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{NeonAuthOptions.SectionName}:BaseUrl"] = NeonAuthTestKit.BaseUrl,
                    [$"{HubOptions.SectionName}:DeskUrl"] = deskUrl,
                    [$"{HubOptions.SectionName}:CrmUrl"] = crmUrl,
                    [$"{TwentyOptions.SectionName}:BaseUrl"] = "http://twenty.test",
                    [$"{TwentyOptions.SectionName}:HubSecret"] = "hub-secret",
                    [$"{ChatwootOptions.SectionName}:BaseUrl"] = "http://chatwoot.test",
                    [$"{ChatwootOptions.SectionName}:PlatformToken"] = "platform-token",
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

            await app.StartAsync(TestContext.Current.CancellationToken);

            return new World(app, kit.Token(subject: userId.ToString()), deskWire, crmWire);
        }

        public async Task<HubAppList> AppsAsync()
        {
            var response = await GetAsync($"{HubEndpoints.Pattern}/apps");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<HubAppList>(TestContext.Current.CancellationToken))!;
        }

        public Task<HttpResponseMessage> SignInAsync(string app)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{HubEndpoints.Pattern}/{app}/sign-in");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await app.DisposeAsync();
        }

        private Task<HttpResponseMessage> GetAsync(string route)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }

    private sealed class FixedAccess(AccessGroup[] groups) : IUserAccess
    {
        public ValueTask<IReadOnlyList<AccessGroup>> GroupsOfAsync(Guid userId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<AccessGroup>>(groups);
    }
}
