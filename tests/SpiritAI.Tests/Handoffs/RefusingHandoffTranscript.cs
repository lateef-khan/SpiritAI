using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Tests.Handoffs;

/// <summary>
/// The transcript a host has until AgentCore can append between turns: every append is refused
/// the way the host's own placeholder refuses it.
/// </summary>
internal sealed class RefusingHandoffTranscript : IHandoffTranscript
{
    public ValueTask AppendAsync(string callId, ChatMessage message, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("AgentCore cannot yet append a message outside a turn.");
}
