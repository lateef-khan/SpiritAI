using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Store;

/// <summary>
/// The reads over <c>spirit.handoff</c>: which row is open, and which is the latest.
/// </summary>
internal sealed class HandoffQueries(SpiritDbContext database)
{
    /// <summary>The chat's open row, if it has one. The index allows at most one.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <returns>A query the store may read or update through.</returns>
    public IQueryable<Handoff> Open(string conversationId)
        => database.Handoffs.Where(h => h.ConversationId == conversationId && h.Status != HandoffStatus.Done);

    /// <inheritdoc cref="IHandoffStore.OpenAsync"/>
    public Task<Handoff?> OpenAsync(string conversationId, CancellationToken cancellationToken)
        => Open(conversationId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc cref="IHandoffStore.LatestAsync"/>
    public Task<Handoff?> LatestAsync(string conversationId, CancellationToken cancellationToken)
    {
        // The open row, if any, sorts before every closed one; the closed ones sort newest first.
        return database.Handoffs.AsNoTracking()
            .Where(h => h.ConversationId == conversationId)
            .OrderBy(h => h.Status == HandoffStatus.Done)
            .ThenByDescending(h => h.DoneAt)
            .ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
