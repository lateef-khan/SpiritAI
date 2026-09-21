using Microsoft.Extensions.Caching.Hybrid;

using ZiggyCreatures.Caching.Fusion;

namespace SpiritAI.Caching;

/// <summary>
/// Registers the one cache this host runs on. FusionCache is the engine; the container hands it
/// out as <see cref="HybridCache"/>.
/// </summary>
public static class CacheExtensions
{
    public static IServiceCollection AddSpiritCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFusionCache().AsHybridCache();

        return services;
    }
}
