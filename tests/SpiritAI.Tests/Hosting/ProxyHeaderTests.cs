using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Hosting;
using SpiritAI.PublicChat;

using Xunit;

namespace SpiritAI.Tests.Hosting;

/// <summary>
/// Believing the proxy about who is calling.
/// </summary>
/// <remarks>
/// This exists for one downstream effect: without it every visitor behind Fly shares the proxy's
/// address, so the public endpoint's per-caller limit becomes one bucket for the whole internet.
/// The last test here is the one that would actually be missed.
/// </remarks>
public sealed class ProxyHeaderTests
{
    private const string Public = "/v1/public/chat/completions";

    [Fact]
    public async Task ReadsTheCallerFromFlysHeader()
    {
        using var host = await StartAsync(enabled: true);

        var seen = await AddressSeenAsync(host, "203.0.113.7");

        Assert.Equal("203.0.113.7", seen);
    }

    [Fact]
    public async Task IgnoresTheHeaderWhenTurnedOff()
    {
        using var host = await StartAsync(enabled: false);

        var seen = await AddressSeenAsync(host, "203.0.113.7");

        // Off is the default, and on a host reachable without a proxy it must stay that way: the
        // header is the caller's to write.
        Assert.NotEqual("203.0.113.7", seen);
    }

    [Fact]
    public async Task GivesEachVisitorTheirOwnAllowance()
    {
        using var host = await StartAsync(enabled: true, permits: 2);
        var client = host.GetTestClient();

        // One visitor spends their allowance and is refused.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "198.51.100.1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "198.51.100.1")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await GetAsync(client, "198.51.100.1")).StatusCode);

        // A second visitor is untouched by the first. Without the forwarded header both would look
        // like the proxy and this would already be a 429.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "198.51.100.2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "198.51.100.2")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await GetAsync(client, "198.51.100.2")).StatusCode);
    }

    [Fact]
    public async Task WithoutTheHeaderEveryoneSharesOneBucket()
    {
        using var host = await StartAsync(enabled: false, permits: 2);
        var client = host.GetTestClient();

        await GetAsync(client, "198.51.100.1");
        await GetAsync(client, "198.51.100.1");

        // The behaviour this feature exists to fix, pinned so the fix cannot quietly regress.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await GetAsync(client, "198.51.100.2")).StatusCode);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Public);
        request.Headers.TryAddWithoutValidation("Fly-Client-IP", clientIp);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>What the host thinks the caller's address is, as the pipeline sees it.</summary>
    private static async Task<string?> AddressSeenAsync(IHost host, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.TryAddWithoutValidation("Fly-Client-IP", clientIp);

        var response = await host.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IHost> StartAsync(bool enabled, int permits = 100)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{ProxyHeaderOptions.SectionName}:Enabled"] = enabled ? "true" : "false",
            [$"{PublicChatOptions.SectionName}:PermitsPerWindow"] = permits.ToString(),
            [$"{PublicChatOptions.SectionName}:WindowSeconds"] = "60",
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddProxyHeaders(configuration);
                    services.AddPublicChat(configuration);
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseProxyHeaders();
                    app.UseRateLimiter();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet(Public, () => Results.Ok("public"));
                        endpoints.MapGet(
                            "/whoami",
                            (HttpContext context) => Results.Content(
                                context.Connection.RemoteIpAddress?.ToString() ?? ""));
                    });
                }))
            .StartAsync(TestContext.Current.CancellationToken);
    }
}
