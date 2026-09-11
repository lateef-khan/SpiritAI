using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Store;

namespace SpiritAI.Tests.Handoffs;

/// <summary>
/// An <see cref="IHandoffStore"/> over a list, keeping the same promises the table does: one open
/// row per chat, positions by ask order, and a claim that only a waiting row can win.
/// </summary>
internal sealed class FakeHandoffStore(TimeProvider clock) : IHandoffStore
{
    private long _nextId = 1;

    /// <summary>Every row ever made, open and closed.</summary>
    public List<Handoff> Rows { get; } = [];

    public Task<HandoffTicket> AskAsync(
        string callId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        var row = Open(callId);

        if (row is null)
        {
            row = new Handoff
            {
                Id = _nextId++,
                CallId = callId,
                Status = HandoffStatus.Waiting,
                AskedBy = askedBy,
                Reason = reason,
                AskedAt = clock.GetUtcNow(),
            };

            Rows.Add(row);
        }

        return Task.FromResult(new HandoffTicket(row, row.Status == HandoffStatus.Waiting ? PositionOf(row) : 0));
    }

    public Task<int?> PositionAsync(string callId, CancellationToken cancellationToken)
        => Task.FromResult<int?>(Open(callId) is { Status: HandoffStatus.Waiting } row ? PositionOf(row) : null);

    public Task<Handoff?> OpenAsync(string callId, CancellationToken cancellationToken)
        => Task.FromResult(Open(callId));

    public Task<Handoff?> LatestAsync(string callId, CancellationToken cancellationToken)
        => Task.FromResult(
            Open(callId)
            ?? Rows.Where(h => h.CallId == callId).OrderByDescending(h => h.DoneAt).ThenByDescending(h => h.Id).FirstOrDefault());

    public Task<IReadOnlyList<Handoff>> ListAsync(HandoffStatus status, int limit, CancellationToken cancellationToken)
    {
        var rows = Rows.Where(h => h.Status == status);

        var ordered = status == HandoffStatus.Done
            ? rows.OrderByDescending(h => h.DoneAt).ThenByDescending(h => h.Id)
            : rows.OrderBy(h => h.AskedAt).ThenBy(h => h.Id);

        return Task.FromResult<IReadOnlyList<Handoff>>([.. ordered.Take(limit)]);
    }

    public Task<HandoffClaim> ClaimAsync(
        string callId, string staffKey, string staffName, CancellationToken cancellationToken)
    {
        var row = Open(callId);

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

    public Task<bool> DoneAsync(string callId, CancellationToken cancellationToken)
        => Task.FromResult(OnOpen(callId, row =>
        {
            row.Status = HandoffStatus.Done;
            row.DoneAt = clock.GetUtcNow();
        }));

    public Task<bool> SetEmailAsync(string callId, string email, CancellationToken cancellationToken)
        => Task.FromResult(OnOpen(callId, row => row.Email = email));

    private Handoff? Open(string callId)
        => Rows.SingleOrDefault(h => h.CallId == callId && h.Status != HandoffStatus.Done);

    private bool OnOpen(string callId, Action<Handoff> change)
    {
        if (Open(callId) is not { } row)
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
