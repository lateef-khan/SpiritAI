using Microsoft.Extensions.Caching.Hybrid;

namespace SpiritAI.GoTo;

/// <summary>
/// Finds the email of the person who owns a phone line. The whole account is read at once and
/// kept in this server's memory for an hour, so a line added since is unknown until then.
/// </summary>
public sealed class GoToStaffDirectory(IGoToDirectoryApiClient directory, HybridCache cache)
{
    private const string CacheKey = "goto:line-emails";

    /// <summary>Staff emails are personal data, so they never go to the shared second level.</summary>
    private static readonly HybridCacheEntryOptions MemoryOnly = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromHours(1),
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    /// <summary>The email of the line's owner.</summary>
    /// <param name="lineId">The <see cref="GoToCallLine.LineId"/>.</param>
    /// <param name="cancellationToken">Cancels a read of the account.</param>
    /// <returns>The email, or null when the line or its owner's email is unknown.</returns>
    public async Task<string?> FindEmailAsync(string lineId, CancellationToken cancellationToken)
    {
        var emails = await cache.GetOrCreateAsync(CacheKey, ReadAsync, MemoryOnly, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return emails.GetValueOrDefault(lineId);
    }

    private async ValueTask<Dictionary<string, string>> ReadAsync(CancellationToken cancellationToken)
    {
        var owners = await directory.ListLineOwnersAsync(cancellationToken).ConfigureAwait(false);
        var emails = await directory.ListUserEmailsAsync(cancellationToken).ConfigureAwait(false);

        return owners
            .Where(o => emails.ContainsKey(o.UserKey))
            .ToDictionary(o => o.LineId, o => emails[o.UserKey]);
    }
}
