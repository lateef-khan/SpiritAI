using Microsoft.Extensions.AI;

namespace SpiritAI.Handoffs;

/// <summary>
/// Puts a message of the human phase into the chat's own history, <c>call_message</c>.
/// </summary>
public interface IHandoffTranscript
{
    /// <summary>Appends one message to a chat, between turns.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="message">
    /// The words, with the speaker in <see cref="ChatMessage.AdditionalProperties"/> the way section
    /// 4.3 of the spec lays out.
    /// </param>
    /// <param name="cancellationToken">Cancels the append.</param>
    ValueTask AppendAsync(string callId, ChatMessage message, CancellationToken cancellationToken = default);
}
