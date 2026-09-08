using AgentCore.Application.Calls;
using AgentCore.Application.Ports;

namespace SpiritAI.Threads;

/// <summary>
/// Whether the caller in front of us may touch the call they named.
/// </summary>
public static class ThreadOwnership
{
    /// <summary>Reads a call, but only for the caller it belongs to.</summary>
    /// <param name="calls">The store to read.</param>
    /// <param name="callId">The call the request named, which may be anything at all.</param>
    /// <param name="principalKey">The caller's key, from <see cref="CallerPrincipal"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The row, or <see langword="null"/> when there is no such call <em>or</em> it is somebody
    /// else's. The two are not told apart on purpose: a caller who could tell them apart could
    /// guess call ids and learn which ones name a real conversation.
    /// </returns>
    public static async ValueTask<CallRecord?> ReadAsync(
        ICallStore calls,
        string callId,
        string principalKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(callId);
        ArgumentNullException.ThrowIfNull(principalKey);

        var record = await calls.GetAsync(callId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return string.Equals(ThreadEnvelope.OwnerOf(record.Custom), principalKey, StringComparison.Ordinal)
            ? record
            : null;
    }
}
