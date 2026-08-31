using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.PublicChat;

using Xunit;

namespace SpiritAI.Tests.PublicChat;

/// <summary>
/// The limits in front of the one route a stranger may reach.
/// </summary>
/// <remarks>
/// Every request past this door spends model tokens, so these are a bill as much as an abuse
/// surface. A limiter that silently applies to nothing looks exactly like a limiter that works,
/// which is why the negative case below matters as much as the positive one.
/// </remarks>
public sealed class PublicChatLimitTests
{
    private const string Public = "/v1/public/chat/completions";
    private const string Slow = "/v1/public/chat/completions/slow";

    /// <summary>Long enough that three requests sent together overlap, short enough not to drag.</summary>
    private static readonly TimeSpan TurnDuration = TimeSpan.FromMilliseconds(750);

    [Fact]
    public async Task RefusesACallerPastTheWindow()
    {
        using var host = await StartAsync(permits: 3);
        var client = host.GetTestClient();

        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.GetAsync(Public, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var refused = await client.GetAsync(Public, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task TellsARefusedCallerWhenToComeBack()
    {
        using var host = await StartAsync(permits: 1);
        var client = host.GetTestClient();

        await client.GetAsync(Public, TestContext.Current.CancellationToken);
        var refused = await client.GetAsync(Public, TestContext.Current.CancellationToken);

        // Without this a caller can only guess, and guessing means retrying into the wall.
        Assert.NotNull(refused.Headers.RetryAfter);
    }

    [Fact]
    public async Task LimitsNothingButThePublicRoute()
    {
        using var host = await StartAsync(permits: 1);
        var client = host.GetTestClient();

        // Spend the public allowance, then show the rest of the host is untouched by it.
        await client.GetAsync(Public, TestContext.Current.CancellationToken);
        await client.GetAsync(Public, TestContext.Current.CancellationToken);

        for (var i = 0; i < 5; i++)
        {
            var signedIn = await client.GetAsync("/v1/chat/completions", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

            var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        }
    }

    [Fact]
    public async Task RefusesMoreTurnsAtOnceThanTheCeiling()
    {
        using var host = await StartAsync(permits: 100, maxConcurrent: 2);
        var client = host.GetTestClient();

        // The slow route holds each turn open for a fixed moment, so three sent together really are
        // three at once. A manual gate released after the third reply deadlocks: the third reply is
        // what the release waits on.
        var turns = await Task.WhenAll(
            client.GetAsync(Slow, TestContext.Current.CancellationToken),
            client.GetAsync(Slow, TestContext.Current.CancellationToken),
            client.GetAsync(Slow, TestContext.Current.CancellationToken));

        Assert.Equal(2, turns.Count(turn => turn.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, turns.Count(turn => turn.StatusCode == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task DisablingTheRouteLimitsNothing()
    {
        using var host = await StartAsync(permits: 1, enabled: false);
        var client = host.GetTestClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync(Public, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>The real limiter over stand-in endpoints. AgentCore's own handler is not the subject.</summary>
    private static async Task<IHost> StartAsync(
        int permits,
        int maxConcurrent = 100,
        bool enabled = true)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{PublicChatOptions.SectionName}:Enabled"] = enabled ? "true" : "false",
            [$"{PublicChatOptions.SectionName}:PermitsPerWindow"] = permits.ToString(),
            [$"{PublicChatOptions.SectionName}:WindowSeconds"] = "60",
            [$"{PublicChatOptions.SectionName}:MaxConcurrentTurns"] = maxConcurrent.ToString(),
        };

        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddPublicChat(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseRateLimiter();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet(Public, () => Results.Ok("public"));
                        endpoints.MapGet(Slow, async () =>
                        {
                            await Task.Delay(TurnDuration);
                            return Results.Ok("slow");
                        });
                        endpoints.MapGet("/v1/chat/completions", () => Results.Ok("private"));
                        endpoints.MapGet("/health", () => Results.Ok("ok"));
                    });
                }))
            .StartAsync(TestContext.Current.CancellationToken);
    }
}
