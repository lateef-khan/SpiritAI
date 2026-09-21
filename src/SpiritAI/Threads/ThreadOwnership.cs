using AgentCore.Application.Conversation;
using AgentCore.Application.Ports;

namespace SpiritAI.Threads;

/// <summary>
/// Whether the caller in front of us may touch the conversation they named.
/// </summary>
public static class ThreadOwnership
{
    /// <summary>Reads a conversation, but only for the caller it belongs to.</summary>
    /// <param name="conversations">The store to read.</param>
    /// <param name="conversationId">The conversation the request named, which may be anything at all.</param>
    /// <param name="principalKey">The caller's key, from <see cref="CallerPrincipal"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The row, or <see langword="null"/> when there is no such conversation <em>or</em> it is somebody
    /// else's. The two are not told apart on purpose: a caller who could tell them apart could
    /// guess conversation ids and learn which ones name a real conversation.
    /// </returns>
    public static async ValueTask<ConversationRecord?> ReadAsync(
        IConversations conversations,
        string conversationId,
        string principalKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(principalKey);

        var record = await conversations.GetAsync(conversationId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return Owns(record, principalKey) ? record : null;
    }

    /// <summary>Whether a row belongs to the caller.</summary>
    /// <param name="record">The row.</param>
    /// <param name="principalKey">The caller's key, from <see cref="CallerPrincipal"/>.</param>
    /// <returns><see langword="true"/> when the caller opened the conversation.</returns>
    public static bool Owns(ConversationRecord record, string principalKey)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(principalKey);

        return string.Equals(ThreadEnvelope.OwnerOf(record.Custom), principalKey, StringComparison.Ordinal);
    }
}
