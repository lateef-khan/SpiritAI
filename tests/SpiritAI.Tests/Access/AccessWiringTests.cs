using System.Net;
using System.Net.Http.Headers;

using AgentCore.Application.Configuration.Parsing;
using AgentCore.AspNetCore.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;
using SpiritAI.Auth;
using SpiritAI.Lookup;
using SpiritAI.Tests.Auth;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>
/// A real Neon token, through the same middleware order as <c>Program.cs</c>, to the rule a route asks
/// for and the entry the chat route runs (roles spec, section 10).
/// </summary>
public sealed class AccessWiringTests
{
    private const string ChatRoute = "/v1/chat/responses";

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

    public static TheoryData<Permission> Every => [.. Permissions.All.Select(info => info.Key)];

    [Theory]
    [MemberData(nameof(Every))]
    public async Task EachPermission_OpensItsOwnGate_AndNoOther(Permission held)
    {
        await using var world = await World.StartAsync(held);

        foreach (var info in Permissions.All)
        {
            var response = await world.GetAsync($"/v1/gate/{Permissions.KeyOf(info.Key)}");
            Assert.Equal(info.Key == held ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/v1/units/1234567890123456", Permission.LookupOrders)]
    [InlineData("/v1/orders/12345", Permission.LookupUnits)]
    public async Task EachLookup_IsRefusedWithoutItsOwnPermission(string route, Permission other)
    {
        await using var world = await World.StartAsync(Permission.ChatAgentStaff, other);

        var response = await world.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(new[] { Permission.ChatAgentDealer }, "dealer")]
    [InlineData(new[] { Permission.ChatAgentGuest }, "main")]
    [InlineData(new[] { Permission.ChatAgentDealer, Permission.ChatAgentManager }, "manager")]
    public async Task TheStrongestAgentHeld_PicksTheEntry(Permission[] held, string entry)
    {
        await using var world = await World.StartAsync(held);

        var response = await world.GetAsync(ChatRoute);

        Assert.Equal(entry, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACallerWithNoAgent_RunsNoEntry()
    {
        await using var world = await World.StartAsync(Permission.LookupUnits);

        var response = await world.GetAsync(ChatRoute);

        Assert.Equal("refused", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private sealed class World(WebApplication app, string token) : IAsyncDisposable
    {
        private readonly HttpClient _client = app.GetTestClient();

        public static async Task<World> StartAsync(params Permission[] held)
        {
            var kit = new NeonAuthTestKit();
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddSingleton(TestHybridCache.Create());
            builder.Services.AddSingleton(ConfigurationLoader.LoadYaml(Document));
            builder.Services.AddNeonAuth(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"{NeonAuthOptions.SectionName}:BaseUrl"] = NeonAuthTestKit.BaseUrl,
                    })
                    .Build());
            builder.Services.AddSingleton(kit.Validator());
            builder.Services.AddAccess();
            builder.Services.AddScoped<IAccessResolver>(_ => new FixedAccess(held));

            // The lookup's own reader, never reached: every lookup test here is refused first.
            builder.Services.AddSingleton(provider => new CachedUnitLookup(
                new UnitLookup((_, _, _) => throw new InvalidOperationException("the lookup was reached.")),
                provider.GetRequiredService<HybridCache>()));

            var app = builder.Build();

            app.UseNeonAuthOnApi();
            app.UseAuthorization();

            foreach (var info in Permissions.All)
            {
                app.MapGet($"/v1/gate/{Permissions.KeyOf(info.Key)}", () => "ok").RequirePermission(info.Key);
            }

            app.MapLookup();
            app.MapGet(ChatRoute, async (HttpContext http) =>
                await AgentCoreEntries.ResolveAsync(http, http.RequestAborted) ?? "refused")
                .SelectEntry<AgentEntrySelector>();

            await app.StartAsync(TestContext.Current.CancellationToken);

            return new World(app, kit.Token(subject: Guid.NewGuid().ToString()));
        }

        public Task<HttpResponseMessage> GetAsync(string route)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await app.DisposeAsync();
        }
    }
}
