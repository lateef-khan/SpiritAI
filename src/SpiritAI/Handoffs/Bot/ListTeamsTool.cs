using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Bot;

/// <summary>Lists the Chatwoot teams a handoff can go to. The list is kept for <see cref="Lifetime"/>.</summary>
public sealed class ListTeamsTool(ChatwootClient chatwoot, HybridCache cache)
{
    /// <summary>How long the list is kept.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Key = "spirit:chatwoot:teams";

    private static readonly HybridCacheEntryOptions Keep = new() { Expiration = Lifetime, LocalCacheExpiration = Lifetime };

    /// <summary>Lists the teams.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every team.</returns>
    public async Task<ListTeamsAnswer> ListAsync(CancellationToken cancellationToken)
        => await cache
            .GetOrCreateAsync(
                Key,
                chatwoot,
                static async (client, token) => new ListTeamsAnswer(await client.ListTeamsAsync(token).ConfigureAwait(false)),
                Keep,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
}
