using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;
using SpiritAI.Database.Configurations;

namespace SpiritAI.Handoffs;

/// <summary>
/// Every move a handoff makes, <c>waiting</c> → <c>human</c> → <c>done</c>, over <c>spirit.handoff</c>.
/// </summary>
public sealed class HandoffStore(SpiritDbContext database, TimeProvider clock)
{
    /// <summary>The most rows one list answers with.</summary>
    public const int MaxListSize = 100;

    private readonly HandoffQueries _queries = new(database);

    /// <summary>
    /// Asks for a person on a chat. A chat that already has an open handoff gets that one back;
    /// two asks racing on one chat both get the row the first one made.
    /// </summary>
    /// <param name="callId">The chat.</param>
    /// <param name="askedBy">Which side asked.</param>
    /// <param name="reason">What the person is for, when the asker said.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>The open row and its place in the line.</returns>
    public async Task<HandoffTicket> AskAsync(
        string callId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        var row = await _queries.OpenAsync(callId, cancellationToken).ConfigureAwait(false)
            ?? await InsertWaitingAsync(callId, askedBy, reason, cancellationToken).ConfigureAwait(false);

        var position = row.Status == HandoffStatus.Waiting
            ? await _queries.PositionOfAsync(row, cancellationToken).ConfigureAwait(false)
            : 0;

        return new HandoffTicket(row, position);
    }

    /// <summary>Where a waiting chat stands in the line.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// One for the front, or <see langword="null"/> when the chat is not waiting: it has no open
    /// handoff, or a person already has it.
    /// </returns>
    public async Task<int?> PositionAsync(string callId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        var row = await _queries.OpenAsync(callId, cancellationToken).ConfigureAwait(false);

        return row is { Status: HandoffStatus.Waiting }
            ? await _queries.PositionOfAsync(row, cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>The chat's open handoff, waiting or with a person.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or <see langword="null"/> when the bot has the chat.</returns>
    public Task<Handoff?> OpenAsync(string callId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        return _queries.OpenAsync(callId, cancellationToken);
    }

    /// <summary>The chat's open handoff if it has one, else the one closed most recently.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or <see langword="null"/> when nobody was ever asked for.</returns>
    public Task<Handoff?> LatestAsync(string callId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        return _queries.LatestAsync(callId, cancellationToken);
    }

    /// <summary>The rows in one state: the queue, the chats being talked to, or the closed ones.</summary>
    /// <param name="status">Which state.</param>
    /// <param name="limit">How many at most, held to one through <see cref="MaxListSize"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// Oldest ask first, the order the queue is served in. Closed rows come newest close first
    /// instead, so the chats just finished are at the top.
    /// </returns>
    public Task<IReadOnlyList<Handoff>> ListAsync(
        HandoffStatus status, int limit, CancellationToken cancellationToken)
        => _queries.ListAsync(status, limit, cancellationToken);

    /// <summary>
    /// Takes a waiting chat for one member of staff. One <c>UPDATE … WHERE</c>: of any number of
    /// claims racing on one chat, exactly one wins.
    /// </summary>
    /// <param name="callId">The chat.</param>
    /// <param name="staffKey">The claimant's caller key.</param>
    /// <param name="staffName">The name the visitor will see.</param>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>How it went, with the row as it stands.</returns>
    public async Task<HandoffClaim> ClaimAsync(
        string callId, string staffKey, string staffName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);
        ArgumentException.ThrowIfNullOrEmpty(staffKey);
        ArgumentException.ThrowIfNullOrEmpty(staffName);

        var now = clock.GetUtcNow();

        var won = await database.Handoffs
            .Where(h => h.CallId == callId && h.Status == HandoffStatus.Waiting && h.AssigneeKey == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(h => h.Status, HandoffStatus.Human)
                    .SetProperty(h => h.AssigneeKey, staffKey)
                    .SetProperty(h => h.AssigneeName, staffName)
                    .SetProperty(h => h.ClaimedAt, now),
                cancellationToken)
            .ConfigureAwait(false) == 1;

        if (won)
        {
            // The newest row this claimant holds on the chat is the one the update just took.
            var taken = await database.Handoffs.AsNoTracking()
                .Where(h => h.CallId == callId && h.AssigneeKey == staffKey)
                .OrderByDescending(h => h.Id)
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);

            return HandoffClaim.Won(taken);
        }

        var open = await _queries.OpenAsync(callId, cancellationToken).ConfigureAwait(false);

        return open is null ? HandoffClaim.NotWaiting() : HandoffClaim.AlreadyTaken(open);
    }

    /// <summary>Closes the chat's open handoff, from waiting or from human. The row stays.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>Whether there was an open handoff to close.</returns>
    public async Task<bool> DoneAsync(string callId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        var now = clock.GetUtcNow();

        return await _queries.Open(callId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(h => h.Status, HandoffStatus.Done)
                    .SetProperty(h => h.DoneAt, now),
                cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <summary>Records where a reply goes when the visitor is not there to read it.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="email">The visitor's address.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the chat had an open handoff to put it on.</returns>
    public async Task<bool> SetEmailAsync(string callId, string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);
        ArgumentException.ThrowIfNullOrEmpty(email);

        return await _queries.Open(callId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Email, email), cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <summary>Marks the visitor as here, now.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the chat had an open handoff to mark.</returns>
    public async Task<bool> TouchVisitorAsync(string callId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        var now = clock.GetUtcNow();

        return await _queries.Open(callId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.VisitorSeenAt, now), cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Inserts the waiting row, or, when another ask got in first, hands back the row it made.
    /// </summary>
    private async Task<Handoff> InsertWaitingAsync(
        string callId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        var row = new Handoff
        {
            CallId = callId,
            Status = HandoffStatus.Waiting,
            AskedBy = askedBy,
            Reason = reason,
            AskedAt = clock.GetUtcNow(),
        };

        database.Handoffs.Add(row);

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException refused) when (IsSecondOpenRow(refused))
        {
            // Left tracked, the context would try the insert again on its next save.
            database.Entry(row).State = EntityState.Detached;

            return await _queries.OpenAsync(callId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"A second open handoff for call '{callId}' was refused, but none can be read.",
                    refused);
        }

        return row;
    }

    /// <summary>Whether the database refused a second open row for one chat.</summary>
    private static bool IsSecondOpenRow(DbUpdateException exception)
        => exception.InnerException is PostgresException { ConstraintName: HandoffConfiguration.OpenPerCallIndex };
}
