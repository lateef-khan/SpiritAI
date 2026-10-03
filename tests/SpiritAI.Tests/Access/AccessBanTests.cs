using System.Net;
using System.Net.Http.Headers;

using AgentCore.Application.Configuration.Parsing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;
using SpiritAI.Auth;
using SpiritAI.Tests.Auth;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>A banned caller is refused on <c>/v1</c> once the cached access is dropped.</summary>
public sealed class AccessBanTests
{
    [Fact]
    public async Task ABannedCaller_Is401_OnTheNextRequestAfterTheCacheIsCleared()
    {
        var access = new FixedAccess(Permission.SettingsPeople);
        await using var host = await Host.StartAsync(access);

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/v1/ping")).StatusCode);

        access.Banned = true;
        await host.Services.GetRequiredService<AccessCache>().ForgetAsync(host.UserId, Cancel);

        var refused = await host.GetAsync("/v1/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task ABannedCaller_IsNotRefused_OnAnOpenRoute()
    {
        var access = new FixedAccess(Permission.SettingsPeople) { Banned = true };
        await using var host = await Host.StartAsync(access);

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/health")).StatusCode);
    }

    [Theory]
    [InlineData("/v1/me", HttpStatusCode.OK)]
    [InlineData("/v1/ME", HttpStatusCode.OK)]
    [InlineData("/v1/me/x", HttpStatusCode.Unauthorized)]
    [InlineData("/v1/meow", HttpStatusCode.Unauthorized)]
    public async Task ABannedCaller_PassesOnlyOnExactlyTheMeRoute(string route, HttpStatusCode status)
    {
        var access = new FixedAccess(Permission.SettingsPeople) { Banned = true };
        await using var host = await Host.StartAsync(access);

        Assert.Equal(status, (await host.GetAsync(route)).StatusCode);
    }

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

    private sealed class Host(WebApplication app, string token, Guid userId) : IAsyncDisposable
    {
        private readonly HttpClient _client = app.GetTestClient();

        public IServiceProvider Services => app.Services;

        public Guid UserId { get; } = userId;

        public static async Task<Host> StartAsync(FixedAccess access)
        {
            var kit = new NeonAuthTestKit();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{NeonAuthOptions.SectionName}:BaseUrl"] = NeonAuthTestKit.BaseUrl,
                })
                .Build();

            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddSingleton(TestHybridCache.Create());
            builder.Services.AddSingleton(ConfigurationLoader.LoadYaml(Document));
            builder.Services.AddNeonAuth(configuration);
            builder.Services.AddSingleton(kit.Validator());
            builder.Services.AddAccess();
            builder.Services.AddScoped<IAccessResolver>(_ => access);

            var app = builder.Build();

            app.UseNeonAuthOnApi();
            app.UseAccessBans();
            app.UseAuthorization();

            app.MapGet("/v1/ping", () => "ok");
            app.MapGet("/v1/me", () => "ok");
            app.MapGet("/v1/me/x", () => "ok");
            app.MapGet("/v1/meow", () => "ok");
            app.MapGet("/health", () => "ok");

            await app.StartAsync(TestContext.Current.CancellationToken);

            var userId = Guid.NewGuid();
            return new Host(app, kit.Token(subject: userId.ToString()), userId);
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
