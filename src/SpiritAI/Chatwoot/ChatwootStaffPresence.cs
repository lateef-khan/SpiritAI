using System.Text.Json;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

using SpiritAI.Handoffs.Desk;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Counts the agents Chatwoot shows online. With auto-offline on, Chatwoot's default, an agent is
/// online only while their dashboard is open; Chatwoot sends no webhook when that changes, so the
/// list is read and kept for <see cref="Lifetime"/>. The service user never opens a dashboard, so
/// it is never counted. A failed read counts nobody and is not kept.
/// </summary>
public sealed class ChatwootStaffPresence(
    ChatwootClient chatwoot,
    HybridCache cache,
    IOptions<ChatwootOptions> options,
    ILogger<ChatwootStaffPresence> logger) : IStaffPresence
{
    /// <summary>How long a count is trusted.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private const string Key = "spirit:chatwoot:staff-online";

    private static readonly HybridCacheEntryOptions Keep = new() { Expiration = Lifetime, LocalCacheExpiration = Lifetime };

    /// <inheritdoc />
    public async Task<int> CountOnlineAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.PresenceEnabled)
        {
            return 0;
        }

        try
        {
            return await cache.GetOrCreateAsync(
                    Key,
                    chatwoot,
                    static async (client, token) => (await client.ListAgentsAsync(token).ConfigureAwait(false)).Count(a => a.Online),
                    Keep,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException
            || (failure is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(failure, "Reading who is online in Chatwoot failed; counting nobody.");
            return 0;
        }
    }
}
