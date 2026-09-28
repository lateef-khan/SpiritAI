namespace SpiritAI.Hub;

/// <summary>
/// Keeps the Hub's own pages, and the root route that opens the Hub, on <see cref="HubOptions.Host"/>
/// alone, and sets the <c>frame-ancestors</c> those pages carry instead of the widget's, the Hub itself may never be framed, and Settings may be framed only by itself, while the
/// staff chat under <c>/chat/</c> keeps the value <see cref="WidgetCspExtensions.UseWidgetFrameAncestors"/>
/// already put there, since the public bubble frames it.
/// </summary>
public static class HubHostExtensions
{
    private const string HubPagePath = "/chat/hub.html";
    private const string HubPageFile = "hub.html";
    private const string SettingsPageFile = "settings.html";

    /// <summary>
    /// Answers 404 for a request whose last path segment is <see cref="HubPageFile"/> or
    /// <see cref="SettingsPageFile"/>, on any host other than <see cref="HubOptions.Host"/>. Matching on
    /// the last segment, rather than the whole path, means a repeated or trailing <c>/</c> cannot slip
    /// the same file past this check under a different spelling. An empty <see cref="HubOptions.Host"/>
    /// serves these pages everywhere. Also overrides the Content-Security-Policy header for the Hub root
    /// and <see cref="HubPageFile"/> to <c>frame-ancestors 'none'</c>, and for <see cref="SettingsPageFile"/>
    /// to <c>frame-ancestors 'self'</c>, regardless of the widget's own allowed origins.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="configuration">Where <see cref="HubOptions.SectionName"/> is read from.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseHubHost(this IApplicationBuilder app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        var hubHost = HostOf(configuration);

        return app.Use(async (context, next) =>
        {
            if (!string.IsNullOrEmpty(hubHost)
                && IsHubOnlyPage(context.Request.Path)
                && !string.Equals(context.Request.Host.Host, hubHost, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (IsHubRoot(context.Request.Path) || IsLastSegment(context.Request.Path, HubPageFile))
            {
                context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            }
            else if (IsLastSegment(context.Request.Path, SettingsPageFile))
            {
                context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self'";
            }

            await next();
        });
    }

    /// <summary>
    /// Maps <c>/</c> to <see cref="HubPagePath"/>, restricted to <see cref="HubOptions.Host"/> when it is
    /// set. Requests for <c>/</c> on any other host fall through, unmatched, to a 404.
    /// </summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <param name="configuration">Where <see cref="HubOptions.SectionName"/> is read from.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapHubPage(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(configuration);

        var hubHost = HostOf(configuration);

        var route = endpoints.MapFallbackToFile("/", HubPagePath);

        if (!string.IsNullOrEmpty(hubHost))
        {
            route.RequireHost(hubHost);
        }

        return endpoints;
    }

    private static string? HostOf(IConfiguration configuration)
        => configuration.GetSection(HubOptions.SectionName)[nameof(HubOptions.Host)];

    private static bool IsHubOnlyPage(PathString path)
        => IsLastSegment(path, HubPageFile) || IsLastSegment(path, SettingsPageFile);

    /// <summary>Whether <paramref name="path"/> is <c>/</c>, the address the Hub itself is served at.</summary>
    private static bool IsHubRoot(PathString path)
        => path.Value is "/" or "";

    private static bool IsLastSegment(PathString path, string fileName)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments is { Length: > 0 } && string.Equals(segments[^1], fileName, StringComparison.OrdinalIgnoreCase);
    }
}
