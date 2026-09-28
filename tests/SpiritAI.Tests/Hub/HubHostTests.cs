using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using SpiritAI.Hosting;
using SpiritAI.Hub;

using Xunit;

namespace SpiritAI.Tests.Hub;

/// <summary>
/// The middleware and routing that keep the Hub's pages on <see cref="HubOptions.Host"/> and let the Hub
/// frame the chat and Settings pages (hub spec, D6).
/// </summary>
public sealed class HubHostTests : IDisposable
{
    private readonly string _wwwroot = Directory.CreateTempSubdirectory("hub-host-tests").FullName;

    public HubHostTests()
    {
        var chat = Path.Combine(_wwwroot, "chat");
        Directory.CreateDirectory(chat);
        File.WriteAllText(Path.Combine(chat, "hub.html"), "<html>hub</html>");
        File.WriteAllText(Path.Combine(chat, "settings.html"), "<html>settings</html>");
        File.WriteAllText(Path.Combine(chat, "index.html"), "<html>index</html>");
    }

    [Fact]
    public async Task TheHubHost_ServesTheHubAtTheRoot()
    {
        await using var app = await StartAsync(hubHost: "hub.spirit.test");

        var response = await GetAsync(app, "hub.spirit.test", "/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<html>hub</html>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheHubRoot_MayNeverBeFramed()
    {
        await using var app = await StartAsync(hubHost: "hub.spirit.test", widgetOrigin: "http://localhost:8899");

        var response = await GetAsync(app, "hub.spirit.test", "/");

        Assert.Equal(
            "frame-ancestors 'none'",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task Settings_MayOnlyBeFramedBySelf_NotByAWidgetOrigin()
    {
        await using var app = await StartAsync(hubHost: "hub.spirit.test", widgetOrigin: "http://localhost:8899");

        var response = await GetAsync(app, "hub.spirit.test", "/chat/settings.html");

        Assert.Equal(
            "frame-ancestors 'self'",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task TheChatHost_DoesNotServeTheHubPages()
    {
        await using var app = await StartAsync(hubHost: "hub.spirit.test");

        var settings = await GetAsync(app, "chat.spirit.test", "/chat/settings.html");
        var root = await GetAsync(app, "chat.spirit.test", "/");

        Assert.Equal(HttpStatusCode.NotFound, settings.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);
    }

    [Fact]
    public async Task ChatPages_MayBeFramedBySelf()
    {
        await using var app = await StartAsync(hubHost: "", widgetOrigin: "http://localhost:8899");

        var response = await GetAsync(app, "chat.spirit.test", "/chat/");

        Assert.Equal(
            "frame-ancestors 'self' http://localhost:8899",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task WithNoHubHost_EveryHostServesTheHub()
    {
        await using var app = await StartAsync(hubHost: "");

        var response = await GetAsync(app, "localhost:5299", "/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<html>hub</html>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/chat//settings.html")]
    [InlineData("/chat/settings.html/")]
    [InlineData("/CHAT/Settings.html")]
    [InlineData("//chat/hub.html")]
    public async Task ADifferentlySpelledHubPagePath_IsAlso404OnTheChatHost(string path)
    {
        await using var app = await StartAsync(hubHost: "hub.spirit.test");

        var context = await SendAsync(app, "chat.spirit.test", path);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task TheHubHost_StillServesSettingsAtItsExactPath()
    {
        await using var app = await StartAsync(hubHost: "hub.spirit.test");

        var context = await SendAsync(app, "hub.spirit.test", "/chat/settings.html");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    public void Dispose() => Directory.Delete(_wwwroot, recursive: true);

    private async Task<WebApplication> StartAsync(string hubHost, string? widgetOrigin = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{HubOptions.SectionName}:{nameof(HubOptions.Host)}"] = hubHost,
        };

        if (widgetOrigin is not null)
        {
            settings[$"{WidgetCspExtensions.OriginsKey}:0"] = widgetOrigin;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Environment.WebRootPath = _wwwroot;
        builder.Environment.WebRootFileProvider = new PhysicalFileProvider(_wwwroot);

        var app = builder.Build();

        app.UseWidgetFrameAncestors(configuration);
        app.UseHubHost(configuration);
        app.UseStaticFiles();
        app.MapFallbackToFile("/chat/{*path:nonfile}", "chat/index.html");
        app.MapHubPage(configuration);

        await app.StartAsync(TestContext.Current.CancellationToken);

        return app;
    }

    private static Task<HttpResponseMessage> GetAsync(WebApplication app, string host, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;
        return app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Sends a request with <paramref name="path"/> set verbatim on <see cref="HttpContext.Request"/>,
    /// bypassing <see cref="Uri"/> parsing so a repeated or leading <c>//</c> reaches the pipeline exactly
    /// as written instead of being normalized away before the request is sent.
    /// </summary>
    private static Task<HttpContext> SendAsync(WebApplication app, string host, string path)
        => app.GetTestServer().SendAsync(
            context =>
            {
                context.Request.Method = HttpMethods.Get;
                context.Request.Host = new HostString(host);
                context.Request.Path = path;
            },
            TestContext.Current.CancellationToken);
}
