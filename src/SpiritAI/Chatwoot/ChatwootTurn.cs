using System.Globalization;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The names an AI turn on a Chatwoot conversation goes by: the headers the widget sends
/// <see cref="ChatwootTurnMiddleware"/>, and the AgentCore id of the AI's copy.
/// </summary>
public static class ChatwootTurn
{
    /// <summary>The header the widget names the Chatwoot conversation's display id in.</summary>
    public const string ConversationHeader = "X-Chatwoot-Conversation";

    /// <summary>The header the widget names the id of the message it just posted to Chatwoot in.</summary>
    public const string MessageHeader = "X-Chatwoot-Message";

    /// <summary>What an AgentCore conversation id made from a Chatwoot conversation starts with.</summary>
    public const string ConversationPrefix = "cw_";

    /// <summary>
    /// The AgentCore conversation id of a Chatwoot conversation. It is made from the <c>uuid</c>,
    /// which stays unique when the Chatwoot database is reset.
    /// </summary>
    /// <param name="uuid">The Chatwoot conversation's <c>uuid</c>.</param>
    /// <returns>The id, such as <c>cw_e86f9a8c-9b8c-4b84-9c3b-16b72acdbea1</c>.</returns>
    public static string AgentCoreIdOf(Guid uuid) => ConversationPrefix + uuid.ToString("D", CultureInfo.InvariantCulture);
}
