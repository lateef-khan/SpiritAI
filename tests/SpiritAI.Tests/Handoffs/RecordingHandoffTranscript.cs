using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Tests.Handoffs;

/// <summary>An <see cref="IHandoffTranscript"/> that keeps what it was given, so a test can read it back.</summary>
internal sealed class RecordingHandoffTranscript : IHandoffTranscript
{
    /// <summary>Every append, in order.</summary>
    public List<(string CallId, ChatMessage Message)> Appended { get; } = [];

    public ValueTask AppendAsync(string callId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        Appended.Add((callId, message));
        return ValueTask.CompletedTask;
    }
}
