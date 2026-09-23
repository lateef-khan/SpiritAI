namespace SpiritAI.Chatwoot;

/// <summary>
/// The Chatwoot conversation a Spirit chat is copied into now, and how far it is copied.
/// </summary>
public sealed class ChatwootLink
{
    /// <summary>The chat, in AgentCore's <c>agentcore.conversation</c>.</summary>
    public required string ConversationId { get; set; }

    /// <summary>The Chatwoot conversation's display id. A new one replaces it once staff resolve the old one.</summary>
    public int ChatwootConversationId { get; set; }

    /// <summary>The last message ordinal copied into Chatwoot, or skipped as not the visitor's or the AI's.</summary>
    public int CopiedThrough { get; set; }

    /// <summary>The handoff Chatwoot was last told about, or <see langword="null"/> before the first.</summary>
    public long? AnnouncedHandoffId { get; set; }

    /// <summary>The visitor email last put in a private note, so a new one is noted once.</summary>
    public string? NotedEmail { get; set; }

    /// <summary>When the row last changed. The database fills it in when left unset.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
