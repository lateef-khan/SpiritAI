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
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary><c>GET /v1/me</c>: who the caller is and what they may do (access spec section 6.5, ruling R1).</summary>
[Collection(PostgresCollection.Name)]
public sealed class MeEndpointsTests(PostgresFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

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

    [Fact]
    public async Task Me_AnswersWhoTheCallerIs_AndWhatTheyHold()
    {
        var userId = await AddUserAsync("Dana Otto");
        await using var host = await Host.StartAsync(fixture, userId, new FixedAccess(Permission.LookupUnits, Permission.ChatAgentStaff));

        var me = await (await host.GetAsync(MeEndpoints.Pattern)).Content.ReadFromJsonAsync<Me>(Cancel);

        Assert.Equal(userId, me!.Id);
        Assert.Equal("Dana Otto", me.Name);
        Assert.False(me.Banned);
        Assert.Equal([Permission.ChatAgentStaff, Permission.LookupUnits], me.Permissions);
        Assert.Equal(Permission.ChatAgentStaff, me.Agent);
    }

    [Fact]
    public async Task ABannedCaller_GetsBannedTrue_WhileOtherRoutesAre401()
    {
        var userId = await AddUserAsync("Banned Person");
        await using var host = await Host.StartAsync(fixture, userId, new FixedAccess(Permission.SettingsPeople) { Banned = true });

        var me = await host.GetAsync(MeEndpoints.Pattern);
        var other = await host.GetAsync("/v1/ping");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<Me>(Cancel);
        Assert.True(body!.Banned);
        Assert.Empty(body.Permissions);
        Assert.Null(body.Agent);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Fact]
    public async Task ATokenForAPersonNeonNoLongerHas_Is401()
    {
        await using var host = await Host.StartAsync(fixture, Guid.NewGuid(), new FixedAccess());

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.GetAsync(MeEndpoints.Pattern)).StatusCode);
    }

    private async Task<Guid> AddUserAsync(string name)
    {
        var userId = Guid.NewGuid();
        await using var db = fixture.Open();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({userId}, {name}, {userId.ToString("N") + "@local.test"}, true)""",
            Cancel);
        return userId;
    }

    private sealed class Host(WebApplication app, string token) : IAsyncDisposable
    {
        private readonly HttpClient _client = app.GetTestClient();

        public static async Task<Host> StartAsync(PostgresFixture fixture, Guid userId, FixedAccess access)
        {
            var kit = new NeonAuthTestKit();
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddSingleton(TestHybridCache.Create());
            builder.Services.AddSingleton(ConfigurationLoader.LoadYaml(Document));
            builder.Services.AddNeonAuth(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [$"{NeonAuthOptions.SectionName}:BaseUrl"] = NeonAuthTestKit.BaseUrl })
                .Build());
            builder.Services.AddSingleton(kit.Validator());
            builder.Services.AddAccess();
            builder.Services.AddScoped<IAccessResolver>(_ => access);
            builder.Services.AddScoped(_ => fixture.Open());

            var app = builder.Build();

            app.UseNeonAuthOnApi();
            app.UseAccessBans();
            app.UseAuthorization();

            app.MapMe();
            app.MapGet("/v1/ping", () => "ok");

            await app.StartAsync(Cancel);

            return new Host(app, kit.Token(subject: userId.ToString()));
        }

        public Task<HttpResponseMessage> GetAsync(string route)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _client.SendAsync(request, Cancel);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await app.DisposeAsync();
        }
    }
}
