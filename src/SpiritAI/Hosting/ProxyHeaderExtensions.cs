using Microsoft.AspNetCore.HttpOverrides;

using Microsoft.Extensions.Options;

namespace SpiritAI.Hosting;

/// <summary>Teaches the host to read the caller's address from the proxy's header.</summary>
public static class ProxyHeaderServiceCollectionExtensions
{
    /// <summary>Binds <see cref="ProxyHeaderOptions"/> and configures the forwarded-headers middleware.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where <see cref="ProxyHeaderOptions.SectionName"/> is read from.</param>
    public static IServiceCollection AddProxyHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ProxyHeaderOptions>()
            .Bind(configuration.GetSection(ProxyHeaderOptions.SectionName));

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ProxyHeaderOptions>>((forwarded, proxy) =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwarded.ForwardedForHeaderName = proxy.Value.ClientIpHeader;

                // One hop. The header names the caller, not a chain, so there is nothing to walk.
                forwarded.ForwardLimit = 1;

                // Cleared, and this is the whole reason the feature is opt-in. The middleware
                // otherwise ignores the header unless the immediate peer is on this list, and Fly's
                // proxy has no address worth pinning: it is internal and it moves. Emptying the list
                // means "believe the header", which is only true when the proxy is the only way in.
                forwarded.KnownProxies.Clear();
                forwarded.KnownIPNetworks.Clear();
            });

        return services;
    }
}

/// <summary>Puts the forwarded-headers middleware in the pipeline, when it is turned on.</summary>
public static class ProxyHeaderApplicationBuilderExtensions
{
    /// <summary>
    /// Rewrites <c>RemoteIpAddress</c> and the scheme from the proxy's headers.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseProxyHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var settings = app.ApplicationServices.GetRequiredService<IOptions<ProxyHeaderOptions>>().Value;

        if (settings.Enabled)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
