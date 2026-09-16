using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SpiritAI.RealTime.Presence;

using StackExchange.Redis;

namespace SpiritAI.RealTime;

/// <summary>Registers the hub, presence, and the backplane, and maps the hub.</summary>
public static class RealTimeExtensions
{
    /// <summary>The prefix the backplane's Redis channels carry, so one Redis can serve more than one app.</summary>
    public const string RedisChannelPrefix = "spirit";

    /// <summary>
    /// Adds SignalR with <see cref="SpiritHub"/>, the Redis backplane when
    /// <see cref="RealTimeOptions.Redis"/> is set, <see cref="IRealTimePublisher"/>,
    /// <see cref="IPresenceStore"/> over the <c>spirit</c> schema, and the sweeper behind it.
    /// Features register their <see cref="IRealTimeAdmission"/> themselves.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where <see cref="RealTimeOptions.SectionName"/> is read from.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddRealTime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(RealTimeOptions.SectionName);

        services.AddOptions<RealTimeOptions>().Bind(section);

        services.TryAddSingleton(TimeProvider.System);

        var signalR = services.AddSignalR();

        if (section[nameof(RealTimeOptions.Redis)] is { Length: > 0 } redis)
        {
            signalR.AddStackExchangeRedis(
                redis,
                options => options.Configuration.ChannelPrefix = RedisChannel.Literal(RedisChannelPrefix));
        }

        services.AddSingleton<IRealTimePublisher, HubRealTimePublisher>();

        services.AddScoped<IPresenceStore, PresenceStore>();

        services.AddHostedService<PresenceSweeper>();

        return services;
    }

    /// <summary>
    /// Maps <see cref="SpiritHub"/> on <see cref="SpiritHub.Pattern"/>, over WebSockets only.
    /// Long polling would need sticky sessions, and Fly's proxy has none.
    /// </summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapRealTime(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHub<SpiritHub>(SpiritHub.Pattern, options => options.Transports = HttpTransportType.WebSockets);

        return endpoints;
    }
}
