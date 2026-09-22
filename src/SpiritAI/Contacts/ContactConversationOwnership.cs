using AgentCore.Application.Conversation;
using AgentCore.Application.Ports;

namespace SpiritAI.Contacts;

/// <summary>
/// Whether a customer (widget visitor) conversation belongs to the caller in front of us.
/// </summary>
public static class ContactConversationOwnership
{
    /// <summary>Reads a conversation, but only for the contact its <c>contact_conversation</c> row names.</summary>
    /// <param name="conversations">The store the row is read from.</param>
    /// <param name="contactConversations">The store <c>contact_conversation</c> is read through.</param>
    /// <param name="contacts">The resolver a channel key is followed to a contact through.</param>
    /// <param name="conversationId">The conversation the request named, which may be anything at all.</param>
    /// <param name="channelKey">The caller's key, such as one <see cref="PublicChat.VisitorPrincipal"/> made.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The row, or <see langword="null"/> when there is no such conversation, it has no
    /// <c>contact_conversation</c> row, <em>or</em> the row names somebody else. The cases are not
    /// told apart on purpose, the same as <see cref="Threads.ThreadOwnership.ReadAsync"/>.
    /// </returns>
    public static async ValueTask<ConversationRecord?> ReadAsync(
        IConversations conversations,
        IContactConversationStore contactConversations,
        IContactResolver contacts,
        string conversationId,
        string channelKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(channelKey);

        var record = await conversations.GetAsync(conversationId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return await OwnsAsync(contactConversations, contacts, conversationId, channelKey, cancellationToken).ConfigureAwait(false)
            ? record
            : null;
    }

    /// <summary>Whether a conversation's <c>contact_conversation</c> row names the caller's contact.</summary>
    /// <param name="contactConversations">The store <c>contact_conversation</c> is read through.</param>
    /// <param name="contacts">The resolver a channel key is followed to a contact through.</param>
    /// <param name="conversationId">The conversation to check. Not proved to exist.</param>
    /// <param name="channelKey">The caller's key, such as one <see cref="PublicChat.VisitorPrincipal"/> made.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> when the row's contact is the caller's own.</returns>
    public static async ValueTask<bool> OwnsAsync(
        IContactConversationStore contactConversations,
        IContactResolver contacts,
        string conversationId,
        string channelKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contactConversations);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(channelKey);

        if (await contactConversations.ContactIdOfAsync(conversationId, cancellationToken).ConfigureAwait(false) is not { } owner)
        {
            return false;
        }

        var caller = await contacts.ResolveAsync(channelKey, cancellationToken).ConfigureAwait(false);

        return owner == caller;
    }
}
