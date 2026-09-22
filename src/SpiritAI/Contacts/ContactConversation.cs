namespace SpiritAI.Contacts;

/// <summary>
/// Which conversation belongs to which person.
/// </summary>
public sealed class ContactConversation
{
    /// <summary>The chat this is about, in AgentCore's <c>agentcore.conversation</c>.</summary>
    public required string ConversationId { get; set; }

    /// <summary>The person this conversation belongs to.</summary>
    public long ContactId { get; set; }

    /// <summary>How this conversation started.</summary>
    public ContactChannel Channel { get; set; }

    /// <summary>When this conversation started. The database fills it in when left unset.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// The last message ordinal the note has read. Null until the first note run. Set by step 4.
    /// </summary>
    public int? NotedThrough { get; set; }
}
