using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// Lists the custom fields a Chatwoot contact can carry. The list is kept for <see cref="Lifetime"/>.
/// </summary>
public sealed class ListContactFieldsTool(ChatwootClient chatwoot, HybridCache cache)
{
    /// <summary>How long the list is kept.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Key = "spirit:chatwoot:contact-fields";

    private static readonly HybridCacheEntryOptions Keep = new() { Expiration = Lifetime, LocalCacheExpiration = Lifetime };

    /// <summary>Lists the fields.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every contact field.</returns>
    public async Task<ListContactFieldsAnswer> ListAsync(CancellationToken cancellationToken)
        => await cache
            .GetOrCreateAsync(
                Key,
                chatwoot,
                static async (client, token) => new ListContactFieldsAnswer(await client.ListContactFieldsAsync(token).ConfigureAwait(false)),
                Keep,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
}
