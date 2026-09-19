using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Store;

/// <summary>
/// The reads over <c>spirit.handoff</c>: which row is open, where it stands, what the queue holds.
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

    /// <inheritdoc cref="IHandoffStore.ListAsync"/>
    public async Task<IReadOnlyList<Handoff>> ListAsync(
        HandoffStatus status, int limit, CancellationToken cancellationToken)
    {
        var rows = database.Handoffs.AsNoTracking().Where(h => h.Status == status);

        var ordered = status == HandoffStatus.Done
            ? rows.OrderByDescending(h => h.DoneAt).ThenByDescending(h => h.Id)
            : rows.OrderBy(h => h.AskedAt).ThenBy(h => h.Id);

        return await ordered
            .Take(Math.Clamp(limit, 1, HandoffStore.MaxListSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Where a waiting row stands: one plus the waiting rows asked before it.</summary>
    /// <param name="row">A row in <see cref="HandoffStatus.Waiting"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One for the front of the line. Asked at the same instant, the lower id is ahead.</returns>
    public async Task<int> PositionOfAsync(Handoff row, CancellationToken cancellationToken)
    {
        var ahead = await database.Handoffs.AsNoTracking()
            .CountAsync(
                h => h.Status == HandoffStatus.Waiting
                    && (h.AskedAt < row.AskedAt || (h.AskedAt == row.AskedAt && h.Id < row.Id)),
                cancellationToken)
            .ConfigureAwait(false);

        return ahead + 1;
    }
}
