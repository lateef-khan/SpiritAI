using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;
using SpiritAI.Database.Configurations;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Store;

/// <summary>
/// The <see cref="IHandoffStore"/> over <c>spirit.handoff</c>, through EF Core.
/// </summary>
public sealed class HandoffStore(SpiritDbContext database, TimeProvider clock) : IHandoffStore
{
    /// <summary>The most rows one list answers with.</summary>
    public const int MaxListSize = 100;

    private readonly HandoffQueries _queries = new(database);

    /// <inheritdoc />
    public async Task<HandoffTicket> AskAsync(
        string conversationId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var row = await _queries.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? await InsertWaitingAsync(conversationId, askedBy, reason, cancellationToken).ConfigureAwait(false);

        var position = row.Status == HandoffStatus.Waiting
            ? await _queries.PositionOfAsync(row, cancellationToken).ConfigureAwait(false)
            : 0;

        return new HandoffTicket(row, position);
    }

    /// <inheritdoc />
    public async Task<int?> PositionAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var row = await _queries.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false);

        return row is { Status: HandoffStatus.Waiting }
            ? await _queries.PositionOfAsync(row, cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <inheritdoc />
    public Task<Handoff?> OpenAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        return _queries.OpenAsync(conversationId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Handoff?> LatestAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        return _queries.LatestAsync(conversationId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Handoff>> ListAsync(
        HandoffStatus status, int limit, CancellationToken cancellationToken)
        => _queries.ListAsync(status, limit, cancellationToken);

    /// <inheritdoc />
    public async Task<HandoffClaim> ClaimAsync(
        string conversationId, string staffKey, string staffName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentException.ThrowIfNullOrEmpty(staffKey);
        ArgumentException.ThrowIfNullOrEmpty(staffName);

        var now = clock.GetUtcNow();

        var won = await database.Handoffs
            .Where(h => h.ConversationId == conversationId && h.Status == HandoffStatus.Waiting && h.AssigneeKey == null)
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
                .Where(h => h.ConversationId == conversationId && h.AssigneeKey == staffKey)
                .OrderByDescending(h => h.Id)
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);

            return HandoffClaim.Won(taken);
        }

        var open = await _queries.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false);

        return open is null ? HandoffClaim.NotWaiting() : HandoffClaim.AlreadyTaken(open);
    }

    /// <inheritdoc />
    public async Task<bool> DoneAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var now = clock.GetUtcNow();

        return await _queries.Open(conversationId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(h => h.Status, HandoffStatus.Done)
                    .SetProperty(h => h.DoneAt, now),
                cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <inheritdoc />
    public async Task<bool> SetEmailAsync(string conversationId, string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentException.ThrowIfNullOrEmpty(email);

        return await _queries.Open(conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Email, email), cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <summary>
    /// Inserts the waiting row, or, when another ask got in first, hands back the row it made.
    /// </summary>
    private async Task<Handoff> InsertWaitingAsync(
        string conversationId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        var row = new Handoff
        {
            ConversationId = conversationId,
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

            return await _queries.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"A second open handoff for conversation '{conversationId}' was refused, but none can be read.",
                    refused);
        }

        return row;
    }

    /// <summary>Whether the database refused a second open row for one chat.</summary>
    private static bool IsSecondOpenRow(DbUpdateException exception)
        => exception.InnerException is PostgresException { ConstraintName: HandoffConfiguration.OpenPerConversationIndex };
}
