namespace SpiritAI.Handoffs.Reads;

/// <summary>
/// Where each member of staff has read up to in each chat, over <c>spirit.conversation_read</c>.
/// </summary>
public interface IConversationReadStore
{
    /// <summary>The reader's marks on a set of chats.</summary>
    /// <param name="staffKey">The reader's caller key.</param>
    /// <param name="conversationIds">The chats to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The seen ordinal by chat. A chat the reader has never opened has no entry.
    /// </returns>
    Task<IReadOnlyDictionary<string, int>> SeenAsync(
        string staffKey, IReadOnlyCollection<string> conversationIds, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the reader's mark to the chat's latest line. A mark never moves backwards, and a chat
    /// with no lines yet gets no mark.
    /// </summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="staffKey">The reader's caller key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task MarkSeenAsync(string conversationId, string staffKey, CancellationToken cancellationToken);
}
