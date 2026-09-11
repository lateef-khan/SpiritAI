namespace SpiritAI.Handoffs.Model;

/// <summary>
/// The one column of AgentCore's <c>public.call</c> that the handoff foreign key points at.
/// </summary>
public sealed class CallStub
{
    /// <summary>The primary key of <c>public.call</c>.</summary>
    public required string CallId { get; set; }
}
