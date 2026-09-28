using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>The route GoTo checks with OPTIONS and posts call events to.</summary>
public sealed class GoToWebhookEndpointTests
{
    private const string Webhook = "/goto/webhook/probe";

    [Fact]
    public async Task OptionsAnswersOkWithAnEmptyBody()
    {
        using var host = await StartAsync(GoToTestServices.Settings);

        var response = await host.GetTestClient().SendAsync(new HttpRequestMessage(HttpMethod.Options, Webhook), Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(Cancel));
    }

    [Fact]
    public async Task AWrongSecretIsNotFoundAndNothingIsQueued()
    {
        using var host = await StartAsync(GoToTestServices.Settings);

        var response = await PostAsync(host, "/goto/webhook/guess", """{"type":"call-events"}""");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await ReceivedAsync(host, TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task NoSecretSetMeansEveryPostIsNotFound()
    {
        using var host = await StartAsync(new() { ["Goto:WebhookSecret"] = "" });

        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(host, "/goto/webhook/", "{}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(host, "/goto/webhook/probe", "{}")).StatusCode);
    }

    [Fact]
    public async Task ABodyOverTheCapIsRefused()
    {
        using var host = await StartAsync(GoToTestServices.Settings);

        var tooBig = $$"""{"pad":"{{new string('x', GoToWebhookEndpoint.MaxBodyBytes)}}"}""";

        var response = await PostAsync(host, Webhook, tooBig);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task AGoodPostIsAnsweredAndTheEventReachesTheQueue()
    {
        using var host = await StartAsync(GoToTestServices.Settings);

        var response = await PostAsync(
            host, Webhook, """{"source":"call-events","type":"call-events","content":{"metadata":{"conversationSpaceId":"c-1"}}}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var received = await ReceivedAsync(host, TimeSpan.FromSeconds(5));
        Assert.Equal("c-1", received?.GetProperty("content").GetProperty("metadata").GetProperty("conversationSpaceId").GetString());
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> PostAsync(IHost host, string path, string body)
        => host.GetTestClient().PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"), Cancel);

    /// <summary>The first queued event, or null when none comes within <paramref name="wait"/>.</summary>
    private static async Task<JsonElement?> ReceivedAsync(IHost host, TimeSpan wait)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(wait);

        try
        {
            await foreach (var callEvent in host.Services.GetRequiredService<GoToCallEventQueue>().ReadAllAsync(timeout.Token))
            {
                return callEvent;
            }
        }
        catch (OperationCanceledException) when (!Cancel.IsCancellationRequested)
        {
        }

        return null;
    }

    private static async Task<IHost> StartAsync(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.Configure<GoToOptions>(configuration.GetSection(GoToOptions.SectionName));
                    services.AddSingleton<GoToCallEventQueue>();
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGoToWebhook());
                }))
            .StartAsync(Cancel);
    }
}
