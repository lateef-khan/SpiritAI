using Microsoft.Extensions.Caching.Hybrid;

namespace SpiritAI.Caching;

/// <summary>
/// The plain read <see cref="HybridCache"/> does not have. Every reader here keeps only a whole
/// answer, so it peeks first and writes afterwards, rather than letting the cache create.
/// </summary>
public static class HybridCacheReads
{
    private static readonly HybridCacheEntryOptions PeekOnly = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData,
    };

    /// <summary>
    /// Reads what the cache holds under a key. A create that is told not to create: the factory
    /// never runs, and a miss is <see langword="null"/>. Both engines honour the flag.
    /// </summary>
    /// <typeparam name="T">What is stored.</typeparam>
    /// <param name="cache">The cache.</param>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry, or <see langword="null"/> when there is none.</returns>
    public static async ValueTask<T?> PeekAsync<T>(this HybridCache cache, string key, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(cache);

        return await cache
            .GetOrCreateAsync(key, static _ => ValueTask.FromResult<T?>(null), PeekOnly, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
