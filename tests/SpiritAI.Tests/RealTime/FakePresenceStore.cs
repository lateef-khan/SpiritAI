using SpiritAI.RealTime.Presence;

// Inside this namespace the bare name Presence is the sibling test namespace, not the row.
using PresenceRow = SpiritAI.RealTime.Presence.Presence;

namespace SpiritAI.Tests.RealTime;

/// <summary>
/// An <see cref="IPresenceStore"/> over a dictionary, with the same window the table has.
/// </summary>
internal sealed class FakePresenceStore(TimeProvider clock, TimeSpan window) : IPresenceStore
{
    private readonly Lock _lock = new();

    private readonly Dictionary<string, PresenceRow> _rows = [];

    private readonly List<string> _touches = [];

    /// <summary>Every socket the hub has said hello for and not yet goodbye.</summary>
    public IReadOnlyCollection<PresenceRow> Rows
    {
        get
        {
            lock (_lock)
            {
                return [.. _rows.Values];
            }
        }
    }

    /// <summary>Every socket touched, in order, once per heartbeat.</summary>
    public IReadOnlyList<string> Touches
    {
        get
        {
            lock (_lock)
            {
                return [.. _touches];
            }
        }
    }

    public Task ConnectAsync(string connectionId, string key, string? name, string kind, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        lock (_lock)
        {
            _rows[connectionId] = new PresenceRow
            {
                ConnectionId = connectionId,
                CallerKey = key,
                CallerName = name,
                Kind = kind,
                ConnectedAt = now,
                SeenAt = now,
            };
        }

        return Task.CompletedTask;
    }

    public Task<bool> TouchAsync(string connectionId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_rows.TryGetValue(connectionId, out var row))
            {
                return Task.FromResult(false);
            }

            row.SeenAt = clock.GetUtcNow();
            _touches.Add(connectionId);
            return Task.FromResult(true);
        }
    }

    public Task DisconnectAsync(string connectionId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _rows.Remove(connectionId);
        }

        return Task.CompletedTask;
    }

    public Task<int> CountOnlineAsync(string kind, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - window;

        lock (_lock)
        {
            return Task.FromResult(
                _rows.Values.Where(r => r.Kind == kind && r.SeenAt >= cutoff).Select(r => r.CallerKey).Distinct().Count());
        }
    }

    public Task<bool> IsOnlineAsync(string key, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - window;

        lock (_lock)
        {
            return Task.FromResult(_rows.Values.Any(r => r.CallerKey == key && r.SeenAt >= cutoff));
        }
    }

    public Task<IReadOnlyList<string>> SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - window;

        lock (_lock)
        {
            var stale = _rows.Values.Where(r => r.SeenAt < cutoff).ToList();

            foreach (var row in stale)
            {
                _rows.Remove(row.ConnectionId);
            }

            return Task.FromResult<IReadOnlyList<string>>([.. stale.Select(r => r.Kind).Distinct()]);
        }
    }
}
