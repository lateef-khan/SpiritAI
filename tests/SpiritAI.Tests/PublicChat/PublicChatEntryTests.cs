using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.PublicChat;

using Xunit;

namespace SpiritAI.Tests.PublicChat;

/// <summary>
/// The public route carries <c>{entry}</c> because AgentCore reads the entry off the URL, but a
/// stranger may reach only the one entry this host serves.
/// </summary>
public sealed class PublicChatEntryTests
{
    [Theory]
    [InlineData("/v1/public/staff/responses")]
    [InlineData("/v1/public/other/responses")]
    public async Task RefusesEveryOtherEntry(string route)
    {
        using var host = await StartAsync();

        var response = await host.GetTestClient().PostAsync(route, content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LetsTheOneEntryThrough()
    {
        using var host = await StartAsync();

        var response = await host.GetTestClient()
            .PostAsync("/v1/public/main/responses", content: null, TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<IHost> StartAsync()
        => await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddPublicChat(new ConfigurationBuilder().Build());
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapPublicChat());
                }))
            .StartAsync(TestContext.Current.CancellationToken);
}
