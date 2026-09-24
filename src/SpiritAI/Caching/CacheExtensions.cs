using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;

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

    /// <summary>
    /// The Redis connection string of the second level. The name is older than the cache: the
    /// deployed secret is set under it (<c>fly secrets set RealTime__Redis=…</c>).
    /// </summary>
    private const string RedisSetting = "RealTime:Redis";

    public static IServiceCollection AddSpiritCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MemoryEntryLimit });

        var fusionCache = services.AddFusionCache()
            .WithMemoryCache(memoryCache)
            .WithDefaultEntryOptions(options => options.SetSize(1));

        if (configuration[RedisSetting] is { Length: > 0 } redis)
        {
            fusionCache.WithDistributedCache(new RedisCache(new RedisCacheOptions { Configuration = redis }));
        }

        fusionCache.AsHybridCache();

        return services;
    }
}
