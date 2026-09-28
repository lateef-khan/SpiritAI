using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Chatwoot;
using SpiritAI.GoTo;

namespace SpiritAI.Handoffs.Callback;

/// <summary>
/// Finds the Chatwoot agent behind a GoTo line: the line's owner in GoTo, matched by email to a
/// member of staff in Chatwoot.
/// </summary>
public sealed class CallStaff(ChatwootClient chatwoot, GoToStaffDirectory directory, HybridCache cache)
{
    private const string AgentsKey = "chatwoot:agents";

    private static readonly HybridCacheEntryOptions AgentsEntry = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    /// <summary>The agent whose line it is.</summary>
    /// <param name="line">The staff line in the call.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The agent, or <see langword="null"/> when the line's owner has no Chatwoot account with the same email.</returns>
    public async Task<ChatwootAgent?> FindAgentAsync(GoToCallLine line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);

        var agents = await cache.GetOrCreateAsync(
                AgentsKey,
                async ct => await chatwoot.ListAgentsAsync(ct).ConfigureAwait(false),
                AgentsEntry,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var email = await directory.FindEmailAsync(line.LineId, cancellationToken).ConfigureAwait(false);

        return agents.FirstOrDefault(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));
    }
}
