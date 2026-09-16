using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpiritAI.Database;

namespace SpiritAI.RealTime.Presence;

/// <summary>
/// The <see cref="IPresenceStore"/> over <c>spirit.presence</c>, through EF Core.
/// </summary>
public sealed class PresenceStore(
    SpiritDbContext database,
    TimeProvider clock,
    IOptions<RealTimeOptions> options) : IPresenceStore
{
    /// <inheritdoc />
    public async Task ConnectAsync(
        string connectionId, string key, string? name, string kind, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(kind);

        var now = clock.GetUtcNow();

        // A connection id is never reused, so a row already under it is a leftover of a socket
        // that closed without a goodbye. Delete then insert keeps the write idempotent.
        await Row(connectionId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        database.Presence.Add(new Presence
        {
            ConnectionId = connectionId,
            CallerKey = key,
            CallerName = name,
            Kind = kind,
            ConnectedAt = now,
            SeenAt = now,
        });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TouchAsync(string connectionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        var now = clock.GetUtcNow();

        return await Row(connectionId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SeenAt, now), cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(string connectionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        return Row(connectionId).ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountOnlineAsync(string kind, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);

        var cutoff = Cutoff();

        return database.Presence
            .Where(p => p.Kind == kind && p.SeenAt >= cutoff)
            .Select(p => p.CallerKey)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> IsOnlineAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var cutoff = Cutoff();

        return database.Presence.AnyAsync(p => p.CallerKey == key && p.SeenAt >= cutoff, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = Cutoff();
        var stale = database.Presence.Where(p => p.SeenAt < cutoff);

        var kinds = await stale.Select(p => p.Kind).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);

        if (kinds.Count == 0)
        {
            return [];
        }

        // A socket touched between the two statements survives the delete; its kind is still
        // announced, with a count that is right either way.
        await stale.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        return kinds;
    }

    private IQueryable<Presence> Row(string connectionId)
        => database.Presence.Where(p => p.ConnectionId == connectionId);

    /// <summary>The moment before which a socket no longer counts as here.</summary>
    private DateTimeOffset Cutoff()
        => clock.GetUtcNow() - TimeSpan.FromSeconds(options.Value.PresenceWindowSeconds);
}
