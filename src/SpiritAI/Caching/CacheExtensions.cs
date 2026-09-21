using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;

using SpiritAI.RealTime;

using ZiggyCreatures.Caching.Fusion;

namespace SpiritAI.Caching;

/// <summary>
/// Registers the one cache this host runs on.
/// </summary>
public static class CacheExtensions
{
    /// <summary>
    /// The L1 (in-memory) entry cap.
    /// </summary>
    private const int MemoryEntryLimit = 10_000;

    public static IServiceCollection AddSpiritCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MemoryEntryLimit });

        var fusionCache = services.AddFusionCache()
            .WithMemoryCache(memoryCache)
            .WithDefaultEntryOptions(options => options.SetSize(1));

        var section = configuration.GetSection(RealTimeOptions.SectionName);
        
        if (section[nameof(RealTimeOptions.Redis)] is { Length: > 0 } redis)
        {
            fusionCache.WithDistributedCache(new RedisCache(new RedisCacheOptions { Configuration = redis }));
        }

        fusionCache.AsHybridCache();

        return services;
    }
}
