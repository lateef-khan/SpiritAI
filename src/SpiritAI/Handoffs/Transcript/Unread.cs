using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

namespace SpiritAI.Handoffs.Transcript;

/// <summary>
/// Whether the visitor said something a reader has not seen: a visitor line stands past the
/// reader's mark. Only the visitor's lines count. Nothing staff, the bot, or the host writes can
/// make a chat unread, so a reply of one's own never puts a dot on it.
/// </summary>
public static class Unread
{
    /// <summary>Reads the transcript against one reader's mark.</summary>
    /// <param name="rows">Every stored message of the conversation. Order does not matter.</param>
    /// <param name="seenOrdinal">The last ordinal the reader has seen, or <see langword="null"/> when they never opened the chat.</param>
    /// <returns><see langword="true"/> when the visitor's latest line is past the mark.</returns>
    public static bool Of(IReadOnlyList<ConversationMessage> rows, int? seenOrdinal)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var visitor = rows.Where(row => row.Content.Role == ChatRole.User).Max(row => (int?)row.Ordinal);

        return visitor is { } latest && (seenOrdinal ?? -1) < latest;
    }
}
