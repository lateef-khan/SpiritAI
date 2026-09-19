using System.Text.Json;

using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;

namespace SpiritAI.Handoffs.Transcript;

/// <summary>
/// Whether the visitor is waiting on a person: they spoke after the last human reply, or no
/// person has replied at all.
/// </summary>
/// <remarks>
/// Only two kinds of line count. The visitor's own, and a member of staff's — a line whose
/// <see cref="SpeakerProperty"/> is <see cref="HandoffSpeaker.HumanKind"/>. The bot's answers and
/// the host's notes ("Dana joined") are neither: the bot answering is why a person was asked
/// for, and a note is not a reply. <c>HandoffQueries.CountAsync</c> asks the same question in
/// SQL, so the two must be kept saying the same thing.
/// </remarks>
public static class ReplyDue
{
    /// <summary>Reads the transcript for whether a reply is owed.</summary>
    /// <param name="rows">Every stored message of the conversation. Order does not matter.</param>
    /// <returns>
    /// <see langword="true"/> when the visitor's latest line is later than any person's. With no
    /// line from the visitor at all there is nothing to reply to.
    /// </returns>
    public static bool Of(IReadOnlyList<ConversationMessage> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var visitor = rows.Where(row => row.Content.Role == ChatRole.User).Max(row => (int?)row.Ordinal);
        var person = rows.Where(row => IsHuman(row.Content)).Max(row => (int?)row.Ordinal);

        return visitor is { } asked && (person ?? -1) < asked;
    }

    private static bool IsHuman(ChatMessage message)
        => SpeakerProperty.Read(message) is { } speaker
            && speaker.TryGetProperty("kind", out var kind)
            && kind.ValueKind == JsonValueKind.String
            && kind.GetString() == HandoffSpeaker.HumanKind;
}
