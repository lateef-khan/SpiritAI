using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

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
/// Where <c>?access_token=</c> is read.
/// </summary>
/// <remarks>
/// A browser WebSocket cannot set a header, so the SignalR JavaScript client puts the token in the
/// query string. A token in a URL lands in every log along the way, so the handler reads it on the
/// hub's path and nowhere else.
/// </remarks>
public sealed class NeonAuthQueryTokenTests
{
    private const string Hub = "/v1/hub";

    private const string Api = "/v1/api";

    [Fact]
    public async Task TheTokenIsReadFromTheQueryOnAListedPath()
    {
        var kit = new NeonAuthTestKit();
        using var host = await StartAsync(kit);

        var response = await host.GetTestClient().GetAsync($"{Hub}?access_token={kit.Token()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("user_123", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheQueryIsIgnoredEverywhereElse()
    {
        var kit = new NeonAuthTestKit();
        using var host = await StartAsync(kit);

        var response = await host.GetTestClient().GetAsync($"{Api}?access_token={kit.Token()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheHeaderWinsWhenBothArePresent()
    {
        var kit = new NeonAuthTestKit();
        using var host = await StartAsync(kit);

        var request = new HttpRequestMessage(HttpMethod.Get, $"{Hub}?access_token={kit.Token(subject: "user_query")}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", kit.Token(subject: "user_header"));

        var response = await host.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("user_header", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>The real registration and middleware, guarding two stand-in routes under <c>/v1</c>.</summary>
    private static Task<IHost> StartAsync(NeonAuthTestKit kit)
        => new HostBuilder()
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
                        options => options.QueryTokenPathPrefixes = [Hub]);

                    services.AddSingleton(kit.Validator());
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseNeonAuthOnApi();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet(Hub, WhoAmI);
                        endpoints.MapGet(Api, WhoAmI);
                    });
                }))
            .StartAsync(TestContext.Current.CancellationToken);

    private static IResult WhoAmI(HttpContext context)
        => Results.Content(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
}
