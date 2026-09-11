namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// The <see cref="ICurrentCall"/> the host runs with until AgentCore tells a binding its call. It
/// lets the host boot, and makes the bot's tool answer that it cannot hand the chat over rather
/// than fail the turn.
/// </summary>
internal sealed class UnavailableCurrentCall : ICurrentCall
{
    /// <inheritdoc />
    public string? CallId => null;
}
