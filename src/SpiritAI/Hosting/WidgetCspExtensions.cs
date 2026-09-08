namespace SpiritAI.Hosting;

/// <summary>Says which pages may put the chat UI in a frame.</summary>
public static class WidgetCspExtensions
{
    /// <summary>Where the allowed embedding origins are read from.</summary>
    public const string OriginsKey = "Widget:AllowedOrigins";

    /// <summary>
    /// Writes <c>Content-Security-Policy: frame-ancestors</c> on every <c>/chat</c> response.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="configuration">Where <see cref="OriginsKey"/> is read from.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseWidgetFrameAncestors(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);

        ArgumentNullException.ThrowIfNull(configuration);

        var origins = configuration.GetSection(OriginsKey).Get<string[]>();

        var frameAncestors = origins is { Length: > 0 }
            ? string.Join(' ', origins)
            : "'none'";

        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/chat"))
            {
                context.Response.Headers.ContentSecurityPolicy = $"frame-ancestors {frameAncestors}";
            }

            await next();
        });
    }
}
