using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;

namespace SpiritAI.Handoffs.Reads;

/// <summary>
/// The <see cref="IConversationReadStore"/> over <c>spirit.conversation_read</c>, through EF Core.
/// </summary>
public sealed class ConversationReadStore(SpiritDbContext database, TimeProvider clock) : IConversationReadStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> SeenAsync(
        string staffKey, IReadOnlyCollection<string> conversationIds, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(staffKey);
        ArgumentNullException.ThrowIfNull(conversationIds);

        if (conversationIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return await database.ConversationReads.AsNoTracking()
            .Where(r => r.StaffKey == staffKey && conversationIds.Contains(r.ConversationId))
            .ToDictionaryAsync(r => r.ConversationId, r => r.SeenOrdinal, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// One statement, so two tabs marking at once cannot lose a mark: the insert reads the chat's
    /// latest ordinal, and on a row already there keeps whichever mark is further on.
    /// </remarks>
    public Task MarkSeenAsync(string conversationId, string staffKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentException.ThrowIfNullOrEmpty(staffKey);

        var now = clock.GetUtcNow();

        return database.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO spirit.conversation_read (conversation_id, staff_key, seen_ordinal, seen_at)
            SELECT {conversationId}, {staffKey}, max(m.ordinal), {now}
            FROM agentcore.conversation_message m
            WHERE m.conversation_id = {conversationId}
            HAVING max(m.ordinal) IS NOT NULL
            ON CONFLICT (conversation_id, staff_key) DO UPDATE
            SET seen_ordinal = GREATEST(conversation_read.seen_ordinal, EXCLUDED.seen_ordinal),
                seen_at = EXCLUDED.seen_at
            """,
            cancellationToken);
    }
}
