namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// Which chat the bound tool is running in. A binding gets its arguments and a cancellation
/// token and nothing else; the call id sits in AgentCore's own turn ambients, which it does not
/// yet expose. Section 7.2 of the handoff spec: however AgentCore comes to say it, this host reads
/// it in one place.
/// </summary>
public interface ICurrentCall
{
    /// <summary>The chat of the turn under way, or <see langword="null"/> when nothing can say.</summary>
    string? CallId { get; }
}
