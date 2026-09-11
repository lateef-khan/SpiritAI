using Microsoft.Extensions.AI;

namespace SpiritAI.Handoffs.Transcript;

/// <summary>
/// The <see cref="IHandoffTranscript"/> the host runs with until AgentCore can append between
/// turns. It lets the host boot, and makes the human path fail where it is used, out loud.
/// </summary>
internal sealed class UnavailableHandoffTranscript : IHandoffTranscript
{
    /// <inheritdoc />
    public ValueTask AppendAsync(string callId, ChatMessage message, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "AgentCore cannot yet append a message outside a turn. "
            + "See docs/superpowers/specs/2026-09-11-human-handoff-design.md section 7.1.");
}
