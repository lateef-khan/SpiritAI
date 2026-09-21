using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Caching;

namespace SpiritAI.Auth.Users;

/// <summary>
/// Remembers who the directory found, so the gate on every staff request reaches the database
/// once per person, not once per request. Nobody is remembered as missing: a person who signs up
/// is found on their next request, and a ban takes hold within <see cref="Lifetime"/>.
/// </summary>
/// <param name="inner">The directory whose answers are remembered.</param>
/// <param name="cache">Where they are remembered.</param>
public sealed class CachedUserDirectory(IUserDirectory inner, HybridCache cache) : IUserDirectory
{
    /// <summary>How long a person is trusted to still have their sign-in.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Prefix = "spirit:user:";

    private static readonly HybridCacheEntryOptions Write = new() { Expiration = Lifetime };

    private readonly IUserDirectory _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    private readonly HybridCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <inheritdoc />
    public async ValueTask<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // The directory matches without regard to case, so two spellings of one address share an entry.
        var key = Prefix + email.ToUpperInvariant();

        if (await _cache.PeekAsync<AuthUser>(key, cancellationToken).ConfigureAwait(false) is { } remembered)
        {
            return remembered;
        }

        var person = await _inner.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (person is not null)
        {
            await _cache.SetAsync(key, person, Write, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return person;
    }
}
