using AgentCore.Application.Ports;

using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// Reads the phone and email on the visitor's own Chatwoot contact, as the visitor, so a visitor
/// who comes back is asked "should we call you at this number again?" and not asked from scratch.
/// </summary>
public sealed class KnownContactTool(IConversations conversations, ChatwootClient chatwoot)
{
    /// <summary>Reads what the contact holds.</summary>
    /// <param name="conversationId">The chat, as AgentCore names it to the binding.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The phone and email, each <see langword="null"/> when missing or when the chat is not a Chatwoot one.</returns>
    public async Task<KnownContactAnswer> ReadAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var record = await conversations.GetAsync(conversationId, cancellationToken).ConfigureAwait(false);

        if (ChatwootIds.Read(record?.Custom) is not { } ids)
        {
            return new KnownContactAnswer(Phone: null, Email: null);
        }

        var contact = await chatwoot.GetVisitorContactAsync(ids.VisitorKey, cancellationToken).ConfigureAwait(false);

        return new KnownContactAnswer(contact.PhoneNumber, contact.Email);
    }
}
