namespace SpiritAI.Handoffs.Model;

/// <summary>
/// The one column of AgentCore's <c>agentcore.conversation</c> that the handoff foreign key points at.
/// </summary>
public sealed class ConversationStub
{
    /// <summary>The primary key of <c>agentcore.conversation</c>.</summary>
    public required string ConversationId { get; set; }
}
