namespace SpiritAI.Contacts;

/// <summary>
/// Records which conversations belong to which contact: <c>spirit.contact_conversation</c>.
/// </summary>
public interface IContactConversationStore
{
    /// <summary>Writes the row for a conversation, unless one is already there.</summary>
    /// <param name="conversationId">The AgentCore conversation.</param>
    /// <param name="contactId">The contact it belongs to, from <see cref="IContactResolver"/>.</param>
    /// <param name="channel">How the conversation started.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task EnsureAsync(string conversationId, long contactId, ContactChannel channel, CancellationToken cancellationToken);

    /// <summary>Reads which contact a conversation's row names.</summary>
    /// <param name="conversationId">The conversation to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row's contact id, or <see langword="null"/> when the conversation has no row.</returns>
    Task<long?> ContactIdOfAsync(string conversationId, CancellationToken cancellationToken);

    /// <summary>Reads a contact's newest conversation on one channel.</summary>
    /// <param name="contactId">The contact.</param>
    /// <param name="channel">The channel the conversation started on.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The conversation that started last, or <see langword="null"/> when there is none.</returns>
    Task<string?> LatestAsync(long contactId, ContactChannel channel, CancellationToken cancellationToken);
}
