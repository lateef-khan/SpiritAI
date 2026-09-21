namespace SpiritAI.Handoffs.Reads;

/// <summary>
/// How far one member of staff has read one chat: <c>spirit.conversation_read</c>.
/// </summary>
/// <remarks>
/// One row per reader per chat, the way Chatwoot keeps <c>agent_last_seen_at</c> on a
/// conversation, except that the mark is each reader's own. A shared mark clears the dot for
/// everyone the moment anyone looks. The mark is an ordinal and not a time: the ordinal is the
/// message table's own key, it only ever goes up, and two lines never share one.
/// </remarks>
public sealed class ConversationRead
{
    /// <summary>The chat, in AgentCore's <c>agentcore.conversation</c>.</summary>
    public required string ConversationId { get; set; }

    /// <summary>The caller key of the reader.</summary>
    public required string StaffKey { get; set; }

    /// <summary>The ordinal of the last line the reader has seen. A visitor line past it is unread.</summary>
    public int SeenOrdinal { get; set; }

    /// <summary>When the mark was last moved.</summary>
    public DateTimeOffset SeenAt { get; set; }
}
