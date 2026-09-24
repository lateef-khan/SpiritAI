using System.Globalization;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The Chatwoot ids of this request's AI turn. <see cref="ChatwootTurnMiddleware"/> fills it once
/// the turn is proved to be the visitor's, before the turn runs.
/// </summary>
public sealed class ChatwootTurn
{
    /// <summary>The header the widget names the Chatwoot conversation's display id in.</summary>
    public const string ConversationHeader = "X-Chatwoot-Conversation";

    /// <summary>The header the widget names the id of the message it just posted to Chatwoot in.</summary>
    public const string MessageHeader = "X-Chatwoot-Message";

    /// <summary>What an AgentCore conversation id made from a Chatwoot conversation starts with.</summary>
    public const string ConversationPrefix = "cw_";

    /// <summary>The visitor's key, which is their contact's <c>source_id</c> in the inbox.</summary>
    public string VisitorKey { get; private set; } = string.Empty;

    /// <summary>The Chatwoot conversation's display id.</summary>
    public int ConversationId { get; private set; }

    /// <summary>The id of the message the visitor just posted to Chatwoot.</summary>
    public int MessageId { get; private set; }

    /// <summary>
    /// The AgentCore conversation id of a Chatwoot conversation. It is made from the <c>uuid</c>,
    /// which stays unique when the Chatwoot database is reset.
    /// </summary>
    /// <param name="uuid">The Chatwoot conversation's <c>uuid</c>.</param>
    /// <returns>The id, such as <c>cw_e86f9a8c-9b8c-4b84-9c3b-16b72acdbea1</c>.</returns>
    public static string AgentCoreIdOf(Guid uuid) => ConversationPrefix + uuid.ToString("D", CultureInfo.InvariantCulture);

    internal void Set(string visitorKey, int conversationId, int messageId)
    {
        VisitorKey = visitorKey;
        ConversationId = conversationId;
        MessageId = messageId;
    }
}
