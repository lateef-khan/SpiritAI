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
}
