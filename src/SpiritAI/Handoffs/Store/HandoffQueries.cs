using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;
using SpiritAI.Handoffs.Contracts;
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
    public async Task<HandoffListing> ListAsync(
        HandoffFilter filter, int limit, HandoffCursor? after, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit, 1, HandoffStore.MaxListSize);

        // One row past the page tells whether there is a page after it, without a second count.
        var rows = await Page(filter, after)
            .Take(size + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count <= size)
        {
            return new HandoffListing(rows, null);
        }

        rows.RemoveAt(size);
        return new HandoffListing(rows, HandoffCursor.After(rows[^1], filter.View));
    }

    /// <inheritdoc cref="IHandoffStore.CountAsync"/>
    public async Task<HandoffCounts> CountAsync(HandoffView view, string staffKey, CancellationToken cancellationToken)
    {
        var counts = await Of(view)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Mine = g.Count(h => h.AssigneeKey == staffKey),
                Unassigned = g.Count(h => h.AssigneeKey == null),
                All = g.Count(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (counts is null)
        {
            return new HandoffCounts(0, 0, 0, 0);
        }

        var awaitingReply = view == HandoffView.Done ? 0 : await AwaitingReplyAsync(staffKey, cancellationToken).ConfigureAwait(false);

        return new HandoffCounts(counts.Mine, counts.Unassigned, counts.All, awaitingReply);
    }

    /// <summary>
    /// How many of the caller's chats have the visitor waiting: the same question
    /// <see cref="Transcript.ReplyDue"/> asks of a transcript, put to every chat at once. AgentCore's
    /// message table is not mapped here, so it is asked in SQL.
    /// </summary>
    private Task<int> AwaitingReplyAsync(string staffKey, CancellationToken cancellationToken)
        => database.Database.SqlQuery<int>(
            $"""
            SELECT count(*)::int AS "Value"
            FROM spirit.handoff h
            WHERE h.status = 'human'
              AND h.assignee_key = {staffKey}
              AND COALESCE((
                    SELECT max(m.ordinal) FROM agentcore.conversation_message m
                    WHERE m.conversation_id = h.conversation_id
                      AND m.content -> 'additionalProperties' -> 'speaker' ->> 'kind' = 'human'), -1)
                < (
                    SELECT max(m.ordinal) FROM agentcore.conversation_message m
                    WHERE m.conversation_id = h.conversation_id AND m.role = 'user')
            """)
            .SingleAsync(cancellationToken);

    /// <summary>The rows of one view, in no order.</summary>
    private IQueryable<Handoff> Of(HandoffView view)
    {
        var rows = database.Handoffs.AsNoTracking();

        return view switch
        {
            HandoffView.Done => rows.Where(h => h.Status == HandoffStatus.Done),
            HandoffView.Waiting => rows.Where(h => h.Status == HandoffStatus.Waiting),
            _ => rows.Where(h => h.Status != HandoffStatus.Done),
        };
    }

    /// <summary>The rows a filter keeps, in its order, from just past <paramref name="after"/>.</summary>
    private IQueryable<Handoff> Page(HandoffFilter filter, HandoffCursor? after)
    {
        var rows = filter.Owner switch
        {
            HandoffOwner.Me => Of(filter.View).Where(h => h.AssigneeKey == filter.StaffKey),
            HandoffOwner.Nobody => Of(filter.View).Where(h => h.AssigneeKey == null),
            _ => Of(filter.View),
        };

        var ascending = filter.Order == HandoffOrder.OldestFirst;

        if (filter.View == HandoffView.Done)
        {
            if (after is { } c)
            {
                rows = ascending
                    ? rows.Where(h => h.DoneAt > c.At || (h.DoneAt == c.At && h.Id > c.Id))
                    : rows.Where(h => h.DoneAt < c.At || (h.DoneAt == c.At && h.Id < c.Id));
            }

            return ascending
                ? rows.OrderBy(h => h.DoneAt).ThenBy(h => h.Id)
                : rows.OrderByDescending(h => h.DoneAt).ThenByDescending(h => h.Id);
        }

        if (after is { } cursor)
        {
            rows = ascending
                ? rows.Where(h => h.AskedAt > cursor.At || (h.AskedAt == cursor.At && h.Id > cursor.Id))
                : rows.Where(h => h.AskedAt < cursor.At || (h.AskedAt == cursor.At && h.Id < cursor.Id));
        }

        return ascending
            ? rows.OrderBy(h => h.AskedAt).ThenBy(h => h.Id)
            : rows.OrderByDescending(h => h.AskedAt).ThenByDescending(h => h.Id);
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
