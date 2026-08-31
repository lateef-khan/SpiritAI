using System.Globalization;
using System.Threading.RateLimiting;

using AgentCore.AspNetCore.Endpoints;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SpiritAI.PublicChat;

/// <summary>Registers the public widget endpoint and the limiter in front of it.</summary>
public static class PublicChatServiceCollectionExtensions
{
    /// <summary>Adds the public chat options and the rate limiter they configure.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where <see cref="PublicChatOptions.SectionName"/> is read from.</param>
    public static IServiceCollection AddPublicChat(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PublicChatOptions>()
            .Bind(configuration.GetSection(PublicChatOptions.SectionName));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Retry-After turns a refusal into something a caller can act on rather than retry
            // against. The widget shows it; a script that ignores it is refused again.
            limiter.OnRejected = static (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)after.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                return ValueTask.CompletedTask;
            };
        });

        // Configured after AddRateLimiter, because the options it needs are only resolvable once
        // the container is built.
        services.AddSingleton<IConfigureOptions<RateLimiterOptions>, ConfigurePublicChatLimiter>();

        return services;
    }
}

/// <summary>Builds the two limits the public route runs under.</summary>
internal sealed class ConfigurePublicChatLimiter(IOptions<PublicChatOptions> options)
    : IConfigureOptions<RateLimiterOptions>
{
    private const string Unpartitioned = "public-chat";

    public void Configure(RateLimiterOptions limiter)
    {
        ArgumentNullException.ThrowIfNull(limiter);

        var settings = options.Value;

        limiter.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context => PerCaller(context, settings)),
            PartitionedRateLimiter.Create<HttpContext, string>(context => InTotal(context, settings)));
    }

    private static RateLimitPartition<string> PerCaller(HttpContext context, PublicChatOptions settings)
    {
        if (!IsPublicChat(context, settings))
        {
            return RateLimitPartition.GetNoLimiter(Unpartitioned);
        }

        return RateLimitPartition.GetFixedWindowLimiter(CallerKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitsPerWindow,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            // Zero: a caller over the limit is told so now. Holding the request open until a permit
            // frees would spend a connection to deliver the same answer later.
            QueueLimit = 0,
        });
    }

    private static RateLimitPartition<string> InTotal(HttpContext context, PublicChatOptions settings)
    {
        if (!IsPublicChat(context, settings))
        {
            return RateLimitPartition.GetNoLimiter(Unpartitioned);
        }

        return RateLimitPartition.GetConcurrencyLimiter(Unpartitioned, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = settings.MaxConcurrentTurns,
            QueueLimit = 0,
        });
    }

    private static bool IsPublicChat(HttpContext context, PublicChatOptions settings)
        => settings.Enabled
            && context.Request.Path.StartsWithSegments(settings.Pattern, StringComparison.OrdinalIgnoreCase);

    /// <summary>Who the limit counts against.</summary>
    private static string CallerKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>Maps the route the widget posts to.</summary>
public static class PublicChatEndpointExtensions
{
    /// <summary>
    /// Maps a second, unauthenticated chat endpoint, or nothing when the route is disabled.
    /// </summary>
    /// <param name="app">The application to map on.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapPublicChat(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var settings = app.Services.GetRequiredService<IOptions<PublicChatOptions>>().Value;

        if (settings.Enabled)
        {
            app.MapChatCompletions(settings.Pattern);
        }

        return app;
    }
}
