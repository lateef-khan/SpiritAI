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
    private const string StaffRoute = "/v1/staff-only";
    private const string ChatRoute = "/v1/chat/responses";

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

    [Theory]
    [InlineData("/v1/units/1234567890123456")]
    [InlineData("/v1/orders/12345")]
    public async Task AGuest_IsRefusedTheLookup(string route)
    {
        await using var world = await World.StartAsync(AccessGroup.Guest);

        var response = await world.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TechService_IsLetIntoAStaffRoute()
    {
        await using var world = await World.StartAsync(AccessGroup.TechService);

        var response = await world.GetAsync(StaffRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ADealer_RunsTheDealerEntry()
    {
        await using var world = await World.StartAsync(AccessGroup.Dealer);

        var response = await world.GetAsync(ChatRoute);

        Assert.Equal("dealer", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACallerWithNoGroup_RunsNoEntry()
    {
        await using var world = await World.StartAsync();

        var response = await world.GetAsync(ChatRoute);

        Assert.Equal("refused", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private sealed class World(WebApplication app, string token) : IAsyncDisposable
    {
        private readonly HttpClient _client = app.GetTestClient();

        public static async Task<World> StartAsync(params AccessGroup[] groups)
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
            builder.Services.AddScoped<IUserAccess>(_ => new FixedAccess(groups));

            // The lookup's own reader, never reached: every lookup test here is refused first.
            builder.Services.AddSingleton(provider => new CachedUnitLookup(
                new UnitLookup((_, _, _) => throw new InvalidOperationException("the lookup was reached.")),
                provider.GetRequiredService<HybridCache>()));

            var app = builder.Build();

            app.UseNeonAuthOnApi();
            app.UseAuthorization();

            app.MapGet(StaffRoute, () => "ok").RequireAuthorization(AccessPolicies.Staff);
            app.MapLookup();
            app.MapGet(ChatRoute, async (HttpContext http) =>
                await AgentCoreEntries.ResolveAsync(http, http.RequestAborted) ?? "refused")
                .SelectEntry<GroupEntrySelector>();

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

    private sealed class FixedAccess(AccessGroup[] groups) : IUserAccess
    {
        public ValueTask<IReadOnlyList<AccessGroup>> GroupsOfAsync(Guid userId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<AccessGroup>>(groups);
    }
}
