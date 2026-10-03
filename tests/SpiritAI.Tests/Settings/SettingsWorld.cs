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
using SpiritAI.Neon;
using SpiritAI.Settings;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;
using SpiritAI.Twenty;

using Xunit;

namespace SpiritAI.Tests.Settings;

/// <summary>
/// A real host with Settings' routes, signed in as an admin unless the test names the caller's
/// permissions, and helpers that seed the database it reads. Every Settings test file shares it.
/// </summary>
internal sealed class SettingsWorld : IAsyncDisposable
{
    /// <summary>A document that declares every entry a chat agent runs, so the start-up check passes.</summary>
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

    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private readonly PostgresFixture _fixture;
    private readonly NeonAuthTestKit _kit;
    private readonly string _token;

    private Func<HttpRequestMessage, (string? Payload, HttpStatusCode Status)> _deskRoute = _ => (null, HttpStatusCode.OK);
    private Func<HttpRequestMessage, (string? Payload, HttpStatusCode Status)> _crmRoute = _ => (null, HttpStatusCode.OK);
    private Func<HttpRequestMessage, (string? Payload, HttpStatusCode Status)> _neonRoute = _ => ("neon_user_created", HttpStatusCode.Created);

    private SettingsWorld(WebApplication app, PostgresFixture fixture, NeonAuthTestKit kit, Guid callerId, RoutingHandler deskWire, RoutingHandler crmWire, RoutingHandler neonWire)
    {
        _app = app;
        _client = app.GetTestClient();
        _fixture = fixture;
        _kit = kit;
        _token = kit.Token(subject: callerId.ToString());
        CallerId = callerId;
        DeskWire = deskWire;
        CrmWire = crmWire;
        NeonWire = neonWire;
    }

    /// <summary>The signed-in caller: the token's subject, with a Neon user row.</summary>
    public Guid CallerId { get; }

    public RoutingHandler DeskWire { get; }

    public RoutingHandler CrmWire { get; }

    public RoutingHandler NeonWire { get; }

    /// <summary>The id the probed Neon create answers with (<c>Neon/Payloads/neon_user_created.json</c>).</summary>
    public static readonly Guid NeonCreated = new("99aed9ca-a881-4f10-9a52-6bc31196131d");

    /// <summary>The world's caller as the writer sees them: holding every permission.</summary>
    public Caller Admin => new(CallerId, Everything.ToHashSet());

    public static string Unique() => Guid.NewGuid().ToString("N");

    /// <summary>A Chatwoot user id no other test or run uses, since a link's (app, external id) is unique.</summary>
    public static string DeskUserId() => Random.Shared.Next(1_000_000, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static async Task<SettingsWorld> StartAsync(
        PostgresFixture fixture,
        IReadOnlyList<Permission>? callerHolds = null,
        string twentyBaseUrl = "http://twenty.test",
        bool neonSetUp = true)
    {
        var kit = new NeonAuthTestKit();
        SettingsWorld? world = null;
        var deskWire = new RoutingHandler(request => world!._deskRoute(request)) { Folder = "Hub" };
        var crmWire = new RoutingHandler(request => world!._crmRoute(request)) { Folder = "Hub" };
        var neonWire = new RoutingHandler(request => world!._neonRoute(request)) { Folder = "Neon" };

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
                ["Neon:ApiKey"] = neonSetUp ? "key-1" : string.Empty,
                ["Neon:ProjectId"] = "proj-1",
                ["Neon:BranchId"] = "br-1",
                ["Neon:CopyToLocal"] = "true",
            })
            .Build();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(TestHybridCache.Create());
        builder.Services.AddSingleton(ConfigurationLoader.LoadYaml(Document));
        builder.Services.AddNeonAuth(configuration);
        builder.Services.AddSingleton(kit.Validator());
        builder.Services.AddAccess();

        builder.Services.Configure<ChatwootOptions>(configuration.GetSection(ChatwootOptions.SectionName));
        builder.Services.AddScoped(_ => fixture.Open());
        builder.Services.AddHub(configuration);
        builder.Services.AddSettings();
        builder.Services.AddHttpClient<DeskUsers>().ConfigurePrimaryHttpMessageHandler(() => deskWire);
        builder.Services.AddHttpClient<CrmUsers>().ConfigurePrimaryHttpMessageHandler(() => crmWire);
        builder.Services.AddNeon(configuration);
        builder.Services.AddHttpClient<NeonUsers>().ConfigurePrimaryHttpMessageHandler(() => neonWire);

        var app = builder.Build();

        app.UseNeonAuthOnApi();
        app.UseAccessBans();
        app.UseAuthorization();

        app.MapHub();
        app.MapSettings();

        await app.StartAsync(Cancel);

        var callerId = Guid.NewGuid();
        await using (var db = fixture.Open())
        {
            await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({callerId}, {"Admin Caller"}, {Unique() + "@spiritfitness.test"}, true)""",
                Cancel);
        }

        world = new SettingsWorld(app, fixture, kit, callerId, deskWire, crmWire, neonWire);
        if (callerHolds is null)
        {
            await world.MakeAdminAsync(callerId);
        }
        else
        {
            await world.GrantAsync(callerId, [.. callerHolds]);
        }

        return world;
    }

    /// <summary>Swaps what Chatwoot answers from now on.</summary>
    public void DeskAnswers(Func<HttpRequestMessage, (string? Payload, HttpStatusCode Status)> route) => _deskRoute = route;

    /// <summary>Swaps Twenty's answer to every request from now on.</summary>
    public void CrmAnswers(HttpStatusCode status) => _crmRoute = _ => (null, status);

    /// <summary>Swaps what Neon answers from now on.</summary>
    public void NeonAnswers(Func<HttpRequestMessage, (string? Payload, HttpStatusCode Status)> route) => _neonRoute = route;

    public async Task<IReadOnlyList<PersonRow>> PeopleAsync()
    {
        var response = await GetAsync($"{SettingsEndpoints.Pattern}/people");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PersonRow>>(Cancel))!;
    }

    public async Task<PersonRow> RowAsync(Guid personId)
        => (await PeopleAsync()).Single(person => person.Id == personId);

    public Task<HttpResponseMessage> GetAsync(string route) => SendAsync(HttpMethod.Get, route);

    /// <summary>A GET signed in as someone other than the world's caller.</summary>
    public Task<HttpResponseMessage> GetAsAsync(Guid personId, string route)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _kit.Token(subject: personId.ToString()));
        return _client.SendAsync(request, Cancel);
    }

    /// <summary>A scope of the running host, for calling its services directly.</summary>
    public AsyncServiceScope Scope() => _app.Services.CreateAsyncScope();

    public Task<HttpResponseMessage> PostAsync(string route) => SendAsync(HttpMethod.Post, route);

    public Task<HttpResponseMessage> DeleteAsync(string route) => SendAsync(HttpMethod.Delete, route);

    public Task<HttpResponseMessage> PutAsync(string route, object body)
        => SendAsync(HttpMethod.Put, route, JsonContent.Create(body));

    public Task<HttpResponseMessage> PostJsonAsync(string route, object body) => SendAsync(HttpMethod.Post, route, JsonContent.Create(body));

    public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string route, object body)
        => SendAsync(method, route, method == HttpMethod.Get || method == HttpMethod.Delete ? null : JsonContent.Create(body));

    /// <summary>Adds a Neon user made for this test alone, so tests never share a row.</summary>
    public async Task<Guid> AddPersonAsync(string name = "Test Person", string? email = null)
    {
        var personId = Guid.NewGuid();
        email ??= Unique() + "@spiritfitness.test";

        await using var db = _fixture.Open();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({personId}, {name}, {email}, true)""",
            Cancel);

        return personId;
    }

    public async Task DeletePersonAsync(Guid personId)
    {
        await using var db = _fixture.Open();
        await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {personId}""", Cancel);
    }

    /// <summary>Every permission, as a role that is not the built-in Admin would hold them.</summary>
    public static IReadOnlyList<Permission> Everything { get; } = [.. Permissions.All.Select(info => info.Key)];

    /// <summary>Makes a role of its own for this test, holding these permissions.</summary>
    public async Task<Guid> SeedRoleAsync(string name, params Permission[] permissions)
    {
        var roleId = Guid.NewGuid();
        await using var db = _fixture.Open();
        db.Roles.Add(new Role { Id = roleId, Name = $"{name} {Unique()}" });
        db.RolePermissions.AddRange(permissions.Select(permission => new RolePermission { RoleId = roleId, Key = Permissions.KeyOf(permission) }));
        await db.SaveChangesAsync(Cancel);
        return roleId;
    }

    public async Task GiveRoleAsync(Guid personId, Guid roleId)
    {
        await using var db = _fixture.Open();
        db.UserRoles.Add(new UserRole { UserId = personId, RoleId = roleId });
        await db.SaveChangesAsync(Cancel);
    }

    /// <summary>Gives a person a new role holding <paramref name="permissions"/>, and answers its id.</summary>
    public async Task<Guid> GrantAsync(Guid personId, params Permission[] permissions)
    {
        var roleId = await SeedRoleAsync("Role", permissions);
        await GiveRoleAsync(personId, roleId);
        return roleId;
    }

    public Task MakeAdminAsync(Guid personId) => GiveRoleAsync(personId, AdminRole.Id);

    /// <summary>Takes the built-in Admin role from everyone, so a test counts only the admins it makes.</summary>
    public async Task RemoveEveryAdminAsync()
    {
        await using var db = _fixture.Open();
        await db.UserRoles.Where(grant => grant.RoleId == AdminRole.Id).ExecuteDeleteAsync(Cancel);
    }

    public async Task BanAsync(Guid personId)
    {
        await using var db = _fixture.Open();
        db.PersonBans.Add(new PersonBan { UserId = personId, BannedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Cancel);
    }

    public async Task SeedLinkAsync(Guid personId, string app, string externalId, bool ready)
    {
        await using var db = _fixture.Open();
        db.LinkedUsers.Add(new LinkedUser { UserId = personId, App = app, ExternalId = externalId, Ready = ready });
        await db.SaveChangesAsync(Cancel);
    }

    /// <summary>Drops whichever person's Desk link has this Chatwoot user id, since <c>(app, external_id)</c> is unique across runs.</summary>
    public async Task DeleteDeskLinkAsync(string externalId)
    {
        await using var db = _fixture.Open();
        await db.LinkedUsers.Where(link => link.App == HubApps.Desk && link.ExternalId == externalId).ExecuteDeleteAsync(Cancel);
    }

    /// <summary>The <c>detail</c> of a problem answer: the words the admin reads.</summary>
    public static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var problem = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        return problem.RootElement.GetProperty("detail").GetString();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, route) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return _client.SendAsync(request, Cancel);
    }
}
