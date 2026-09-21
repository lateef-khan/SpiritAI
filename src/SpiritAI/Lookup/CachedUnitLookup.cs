using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Caching;

namespace SpiritAI.Lookup;

/// <summary>
/// Remembers what <see cref="UnitLookup"/> read, so a panel opened twice on the same machine
/// reaches DAB once.
/// </summary>
/// <param name="inner">The reader whose answers are remembered.</param>
/// <param name="cache">Where they are remembered.</param>
public sealed class CachedUnitLookup(UnitLookup inner, HybridCache cache)
{
    /// <summary>
    /// How long an answer is trusted. A job closed after the read shows as open until this passes.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string UnitPrefix = "spirit:unit:";

    private const string OrderPrefix = "spirit:order:";

    private static readonly HybridCacheEntryOptions Write = new() { Expiration = Lifetime };

    private readonly UnitLookup _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    private readonly HybridCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <inheritdoc cref="UnitLookup.ReadUnitAsync"/>
    public async Task<UnitDocument?> ReadUnitAsync(string serial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serial);

        var key = UnitPrefix + serial;

        if (await _cache.PeekAsync<UnitDocument>(key, cancellationToken).ConfigureAwait(false) is { } remembered)
        {
            return remembered;
        }

        var unit = await _inner.ReadUnitAsync(serial, cancellationToken).ConfigureAwait(false);

        if (unit is { Unavailable.Count: 0 })
        {
            await _cache.SetAsync(key, unit, Write, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return unit;
    }

    /// <inheritdoc cref="UnitLookup.ReadOrderAsync"/>
    public async Task<OrderDocument?> ReadOrderAsync(string orderNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderNumber);

        var key = OrderPrefix + orderNumber;

        if (await _cache.PeekAsync<OrderDocument>(key, cancellationToken).ConfigureAwait(false) is { } remembered)
        {
            return remembered;
        }

        var order = await _inner.ReadOrderAsync(orderNumber, cancellationToken).ConfigureAwait(false);

        if (order is not null)
        {
            await _cache.SetAsync(key, order, Write, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return order;
    }
}
