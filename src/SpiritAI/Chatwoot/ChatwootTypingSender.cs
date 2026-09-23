using Microsoft.EntityFrameworkCore;

using SpiritAI.Contacts;
using SpiritAI.Database;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Shows the visitor's typing to staff in the chat's Chatwoot conversation. A chat with no
/// conversation yet, or whose contact has no Chatwoot key yet, has nobody to show it to.
/// </summary>
public sealed class ChatwootTypingSender(
    SpiritDbContext database,
    IContactConversationStore contactConversations,
    ChatwootClient chatwoot)
{
    /// <summary>Sends one signal.</summary>
    /// <param name="typing">The signal.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    public async Task SendAsync(VisitorTyping typing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(typing);

        var chatwootConversationId = await database.ChatwootLinks
            .AsNoTracking()
            .Where(l => l.ConversationId == typing.ConversationId)
            .Select(l => (int?)l.ChatwootConversationId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chatwootConversationId is not { } conversationId
            || await contactConversations.ContactIdOfAsync(typing.ConversationId, cancellationToken).ConfigureAwait(false) is not { } contactId)
        {
            return;
        }

        var sourceId = await database.Contacts
            .AsNoTracking()
            .Where(c => c.Id == contactId)
            .Select(c => c.ChatwootSourceId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sourceId is null)
        {
            return;
        }

        await chatwoot.ToggleTypingAsync(sourceId, conversationId, typing.On, cancellationToken).ConfigureAwait(false);
    }
}
