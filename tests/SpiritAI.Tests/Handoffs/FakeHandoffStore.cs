using AgentCore.Application.Ports;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Tests.Handoffs;

/// <summary>
/// An <see cref="IHandoffStore"/> over a list, keeping the same promises the table does: one open
/// row per chat, positions by ask order, and a claim that only a waiting row can win. Given the
/// conversations, it also counts the caller's chats whose visitor is waiting, the way the table's
/// SQL does; without them, that count is zero.
/// </summary>
internal sealed class FakeHandoffStore(TimeProvider clock, IConversations? conversations = null) : IHandoffStore
{
    private long _nextId = 1;

    /// <summary>Every row ever made, open and closed.</summary>
    public List<Handoff> Rows { get; } = [];

    public Task<HandoffTicket> AskAsync(
        string conversationId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        var row = Open(conversationId);

        if (row is null)
        {
            row = new Handoff
            {
                Id = _nextId++,
                ConversationId = conversationId,
                Status = HandoffStatus.Waiting,
                AskedBy = askedBy,
                Reason = reason,
                AskedAt = clock.GetUtcNow(),
            };

            Rows.Add(row);
        }

        return Task.FromResult(new HandoffTicket(row, row.Status == HandoffStatus.Waiting ? PositionOf(row) : 0));
    }

    public Task<int?> PositionAsync(string conversationId, CancellationToken cancellationToken)
        => Task.FromResult<int?>(Open(conversationId) is { Status: HandoffStatus.Waiting } row ? PositionOf(row) : null);

    public Task<Handoff?> OpenAsync(string conversationId, CancellationToken cancellationToken)
        => Task.FromResult(Open(conversationId));

    public Task<Handoff?> LatestAsync(string conversationId, CancellationToken cancellationToken)
        => Task.FromResult(
            Open(conversationId)
            ?? Rows.Where(h => h.ConversationId == conversationId).OrderByDescending(h => h.DoneAt).ThenByDescending(h => h.Id).FirstOrDefault());

    public Task<HandoffListing> ListAsync(
        HandoffFilter filter, int limit, HandoffCursor? after, CancellationToken cancellationToken)
    {
        var rows = filter.Owner switch
        {
            HandoffOwner.Me => Of(filter.View).Where(h => h.AssigneeKey == filter.StaffKey),
            HandoffOwner.Nobody => Of(filter.View).Where(h => h.AssigneeKey == null),
            _ => Of(filter.View),
        };

        var ordered = (filter.View, filter.Order) switch
        {
            (HandoffView.Done, HandoffOrder.OldestFirst) => rows.OrderBy(h => h.DoneAt).ThenBy(h => h.Id),
            (HandoffView.Done, _) => rows.OrderByDescending(h => h.DoneAt).ThenByDescending(h => h.Id),
            (_, HandoffOrder.OldestFirst) => rows.OrderBy(h => h.AskedAt).ThenBy(h => h.Id),
            _ => rows.OrderByDescending(h => h.AskedAt).ThenByDescending(h => h.Id),
        };

        // The cursor names the last row handed out; the page is whatever follows it in this order.
        var page = after is { } cursor
            ? ordered.SkipWhile(h => HandoffCursor.After(h, filter.View) != cursor).Skip(1)
            : ordered;

        var taken = page.Take(limit + 1).ToList();
        var next = taken.Count > limit ? HandoffCursor.After(taken[limit - 1], filter.View) : (HandoffCursor?)null;

        return Task.FromResult(new HandoffListing([.. taken.Take(limit)], next));
    }

    public async Task<HandoffCounts> CountAsync(HandoffView view, string staffKey, CancellationToken cancellationToken)
    {
        var rows = Of(view).ToList();
        var awaitingReply = 0;

        if (conversations is not null && view != HandoffView.Done)
        {
            foreach (var row in rows.Where(h => h.Status == HandoffStatus.Human && h.AssigneeKey == staffKey))
            {
                if (ReplyDue.Of(await conversations.AllAsync(row.ConversationId, cancellationToken)))
                {
                    awaitingReply++;
                }
            }
        }

        return new HandoffCounts(
            rows.Count(h => h.AssigneeKey == staffKey),
            rows.Count(h => h.AssigneeKey == null),
            rows.Count,
            awaitingReply);
    }

    private IEnumerable<Handoff> Of(HandoffView view)
        => view switch
        {
            HandoffView.Done => Rows.Where(h => h.Status == HandoffStatus.Done),
            HandoffView.Waiting => Rows.Where(h => h.Status == HandoffStatus.Waiting),
            _ => Rows.Where(h => h.Status != HandoffStatus.Done),
        };

    public Task<HandoffClaim> ClaimAsync(
        string conversationId, string staffKey, string staffName, CancellationToken cancellationToken)
    {
        var row = Open(conversationId);

        if (row is null)
        {
            return Task.FromResult(HandoffClaim.NotWaiting());
        }

        if (row.Status != HandoffStatus.Waiting || row.AssigneeKey is not null)
        {
            return Task.FromResult(HandoffClaim.AlreadyTaken(row));
        }

        row.Status = HandoffStatus.Human;
        row.AssigneeKey = staffKey;
        row.AssigneeName = staffName;
        row.ClaimedAt = clock.GetUtcNow();

        return Task.FromResult(HandoffClaim.Won(row));
    }

    public Task<bool> DoneAsync(string conversationId, CancellationToken cancellationToken)
        => Task.FromResult(OnOpen(conversationId, row =>
        {
            row.Status = HandoffStatus.Done;
            row.DoneAt = clock.GetUtcNow();
        }));

    public Task<bool> SetEmailAsync(string conversationId, string email, CancellationToken cancellationToken)
        => Task.FromResult(OnOpen(conversationId, row => row.Email = email));

    private Handoff? Open(string conversationId)
        => Rows.SingleOrDefault(h => h.ConversationId == conversationId && h.Status != HandoffStatus.Done);

    private bool OnOpen(string conversationId, Action<Handoff> change)
    {
        if (Open(conversationId) is not { } row)
        {
            return false;
        }

        change(row);
        return true;
    }

    private int PositionOf(Handoff row)
        => 1 + Rows.Count(h =>
            h.Status == HandoffStatus.Waiting
            && (h.AskedAt < row.AskedAt || (h.AskedAt == row.AskedAt && h.Id < row.Id)));
}
