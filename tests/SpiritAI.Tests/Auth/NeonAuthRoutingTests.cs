using System.Net;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;

using Xunit;

namespace SpiritAI.Tests.Auth;

/// <summary>
/// Which doors are locked.
/// </summary>
/// <remarks>
/// The validator can be perfect and the host still wide open if the middleware guards the wrong
/// paths — or shut if it guards too many, which locks a signed-out visitor out of the page they
/// need in order to sign in. Neither mistake shows up in a unit test of the token, so these send
/// real requests through a real pipeline.
/// </remarks>
public sealed class NeonAuthRoutingTests
{
    [Theory]
    // Open: the liveness probe, the built chat bundle, and above all the sign-in page.
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/chat/login.html", HttpStatusCode.OK)]
    [InlineData("/chat/", HttpStatusCode.OK)]
    // Closed: everything the agent answers on.
    [InlineData("/v1/chat/completions", HttpStatusCode.Unauthorized)]
    [InlineData("/v1/call", HttpStatusCode.Unauthorized)]
    [InlineData("/v1", HttpStatusCode.Unauthorized)]
    public async Task WithoutAToken(string path, HttpStatusCode expected)
    {
        using var host = await StartAsync();

        var response = await host.GetTestClient().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AnOpenPrefixIsCarvedOutOfAGuardedOne()
    {
        using var host = await StartAsync(openPrefixes: ["/v1/public/chat/completions"]);

        // The widget's route. It sits under /v1 with everything else and must still answer a
        // stranger, or the public bubble 401s on every message.
        var response = await host.GetTestClient().GetAsync("/v1/public/chat/completions", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheCarveOutDoesNotOpenTheRestOfTheApi()
    {
        using var host = await StartAsync(openPrefixes: ["/v1/public/chat/completions"]);

        var response = await host.GetTestClient().GetAsync("/v1/chat/completions", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AProtectedPrefixDoesNotSwallowANeighbouringPath()
    {
        using var host = await StartAsync();

        // "/v1" must guard "/v1/..." and stop there. A plain StartsWith would also catch "/v1x".
        var response = await host.GetTestClient().GetAsync("/v1x/open", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ARefusalSaysHowToTryAgain()
    {
        using var host = await StartAsync();

        var response = await host.GetTestClient().GetAsync("/v1/chat/completions", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("Basic abc123")]
    [InlineData("Bearer ")]
    [InlineData("Bearer rubbish")]
    [InlineData("Bearer a.b.c")]
    public async Task AnUnusableAuthorizationHeaderIsStillA401(string header)
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/chat/completions");
        request.Headers.TryAddWithoutValidation("Authorization", header);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AValidTokenGetsThrough()
    {
        var kit = new NeonAuthTestKit();
        using var host = await StartAsync(kit);
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", kit.Token());

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("user_123", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The real registration and the real middleware, over stand-in endpoints.
    /// </summary>
    /// <remarks>
    /// AgentCore's own endpoints are not mapped here: booting it needs a model provider and a
    /// configuration document, and what is under test is which paths the lock covers, not what
    /// answers behind it.
    /// </remarks>
    private static async Task<IHost> StartAsync(NeonAuthTestKit? kit = null, string[]? openPrefixes = null)
    {
        kit ??= new NeonAuthTestKit();

        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddNeonAuth(
                        new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                [$"{NeonAuthOptions.SectionName}:BaseUrl"] = NeonAuthTestKit.BaseUrl,
                            })
                            .Build(),
                        options => options.OpenPathPrefixes = openPrefixes ?? []);

                    // The one seam the test needs: the same validator, wired to the test's key set
                    // rather than to the real Neon.
                    services.AddSingleton(kit.Validator());
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseNeonAuthOnApi();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/health", () => Results.Ok("ok"));
                        endpoints.MapGet("/chat/login.html", () => Results.Content("<html></html>", "text/html"));
                        endpoints.MapGet("/chat/", () => Results.Content("<html></html>", "text/html"));
                        endpoints.MapGet("/v1x/open", () => Results.Ok("open"));
                        endpoints.MapGet("/v1", () => Results.Ok("root"));
                        endpoints.MapGet("/v1/call", () => Results.Ok("call"));
                        endpoints.MapGet("/v1/public/chat/completions", () => Results.Ok("public"));
                        endpoints.MapGet(
                            "/v1/chat/completions",
                            (HttpContext context) => Results.Content(
                                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"));
                    });
                }))
            .StartAsync(TestContext.Current.CancellationToken);

        return host;
    }
}
