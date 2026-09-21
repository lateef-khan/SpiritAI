using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using SpiritAI.Threads;

namespace SpiritAI.Tests;

/// <summary>
/// Reads a test conversation's words through the door, which hands out windows and never the
/// whole. A test conversation is a few turns, so the widest window the routes allow is all of it.
/// </summary>
internal static class ConversationWords
{
    /// <summary>Every word of a conversation, oldest first.</summary>
    public static async Task<IReadOnlyList<ConversationMessage>> AllAsync(
        this IConversations conversations, string conversationId, CancellationToken cancellationToken)
    {
        var stored = await conversations.LoadWindowAsync(conversationId, new TranscriptWindow(null, HistoryWindow.MaxTurns), cancellationToken);
        return stored?.Messages ?? [];
    }
}
