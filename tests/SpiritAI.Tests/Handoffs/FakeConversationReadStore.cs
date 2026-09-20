using AgentCore.Application.Ports;

using SpiritAI.Handoffs.Reads;

namespace SpiritAI.Tests.Handoffs;

/// <summary>
/// An <see cref="IConversationReadStore"/> over a dictionary, keeping the table's one promise: a
/// mark moves to the chat's latest line and never back.
/// </summary>
internal sealed class FakeConversationReadStore(IConversations conversations) : IConversationReadStore
{
    /// <summary>Every mark, by chat and reader.</summary>
    public Dictionary<(string ConversationId, string StaffKey), int> Marks { get; } = [];

    public Task<IReadOnlyDictionary<string, int>> SeenAsync(
        string staffKey, IReadOnlyCollection<string> conversationIds, CancellationToken cancellationToken)
    {
        var seen = Marks
            .Where(entry => entry.Key.StaffKey == staffKey && conversationIds.Contains(entry.Key.ConversationId))
            .ToDictionary(entry => entry.Key.ConversationId, entry => entry.Value, StringComparer.Ordinal);

        return Task.FromResult<IReadOnlyDictionary<string, int>>(seen);
    }

    public async Task MarkSeenAsync(string conversationId, string staffKey, CancellationToken cancellationToken)
    {
        var rows = await conversations.AllAsync(conversationId, cancellationToken);

        if (rows.Count == 0)
        {
            return;
        }

        var latest = rows.Max(row => row.Ordinal);
        var key = (conversationId, staffKey);

        Marks[key] = Marks.TryGetValue(key, out var mark) ? Math.Max(mark, latest) : latest;
    }
}
