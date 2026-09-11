namespace SpiritAI.RealTime.Presence;

/// <summary>
/// Every open socket, over <c>spirit.presence</c>. In the database and not in memory, because a
/// Machine that dies cannot say goodbye for its sockets: a sweep of stale rows says it.
/// </summary>
public interface IPresenceStore
{
    /// <summary>Records that a socket opened. A row already there for the id is replaced.</summary>
    /// <param name="connectionId">The socket.</param>
    /// <param name="key">The caller key of whoever is on it.</param>
    /// <param name="name">Their display name, when they have one.</param>
    /// <param name="kind">Which kind of caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task ConnectAsync(string connectionId, string key, string? name, string kind, CancellationToken cancellationToken);

    /// <summary>Marks a socket as alive, now.</summary>
    /// <param name="connectionId">The socket.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether there was a row to touch.</returns>
    Task<bool> TouchAsync(string connectionId, CancellationToken cancellationToken);

    /// <summary>Records that a socket closed.</summary>
    /// <param name="connectionId">The socket.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task DisconnectAsync(string connectionId, CancellationToken cancellationToken);

    /// <summary>How many callers of one kind are on a socket seen inside the presence window.</summary>
    /// <param name="kind">Which kind.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Distinct caller keys, so two tabs of one person count once.</returns>
    Task<int> CountOnlineAsync(string kind, CancellationToken cancellationToken);

    /// <summary>Whether one caller has any socket seen inside the presence window.</summary>
    /// <param name="key">Their caller key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> while they are here to read a push.</returns>
    Task<bool> IsOnlineAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes the rows of sockets not seen inside the presence window.</summary>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>The kinds that lost rows, each once. Empty when nothing was stale.</returns>
    Task<IReadOnlyList<string>> SweepAsync(CancellationToken cancellationToken);
}
