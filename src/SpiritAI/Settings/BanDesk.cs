using Microsoft.EntityFrameworkCore;

using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.Hub;

namespace SpiritAI.Settings;

/// <summary>
/// The Desk step of a Ban or an Unban, run in the same request once the ban is saved or cleared.
/// </summary>
public sealed class BanDesk(SpiritDbContext db, DeskUsers desk, LinkPerson linker, ILogger<BanDesk> log)
{
    /// <summary>Takes a banned person out of the Chatwoot account and marks their Desk link not ready.</summary>
    public async Task<(StepResult Result, string? Detail)> LeaveAsync(Guid personId, CancellationToken cancellationToken)
    {
        if (await DeskLinkAsync(personId, cancellationToken).ConfigureAwait(false) is not { } link)
        {
            return (StepResult.None, null);
        }

        try
        {
            // Chatwoot answers a leave of a user who is not a member with 200, so a link that is not
            // ready is left too: that finishes a leave an earlier attempt left half done.
            await desk.LeaveAccountAsync(int.Parse(link.ExternalId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ChatwootFailed(ex, cancellationToken))
        {
            log.LogWarning(ex, "Ban could not take Desk user {DeskUserId} out of the account.", link.ExternalId);
            return (StepResult.Failed, "Desk did not answer. Press Ban again to finish.");
        }

        link.Ready = false;

        try
        {
            // Chatwoot has already removed the person, so a cancelled request must not keep the link ready.
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A delete or an unlink dropped the link meanwhile, so nothing is left to mark.
        }

        return (StepResult.Done, null);
    }

    /// <summary>Puts an unbanned person back in the Chatwoot account and the inbox, when their Desk link is not ready.</summary>
    public async Task<(StepResult Result, string? Detail)> RejoinAsync(Guid personId, CancellationToken cancellationToken)
    {
        if (await DeskLinkAsync(personId, cancellationToken).ConfigureAwait(false) is not { Ready: false })
        {
            return (StepResult.None, null);
        }

        try
        {
            return await linker.RejoinDeskAsync(personId, cancellationToken).ConfigureAwait(false)
                ? (StepResult.Done, null)
                : (StepResult.None, null);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // A delete dropped the link while Chatwoot was joining; no row is left to find the member by.
            await LeaveDroppedAsync(ex, cancellationToken).ConfigureAwait(false);
            return (StepResult.None, null);
        }
        catch (Exception ex) when (ChatwootFailed(ex, cancellationToken))
        {
            log.LogWarning(ex, "Unban could not put {PersonId} back in Desk.", personId);
            return (StepResult.Failed, "Desk did not answer. Press Unban again to finish.");
        }
    }

    private Task<LinkedUser?> DeskLinkAsync(Guid personId, CancellationToken cancellationToken)
        => db.LinkedUsers.SingleOrDefaultAsync(link => link.UserId == personId && link.App == HubApps.Desk, cancellationToken);

    private async Task LeaveDroppedAsync(DbUpdateConcurrencyException dropped, CancellationToken cancellationToken)
    {
        var deskUserId = dropped.Entries.Select(entry => entry.Entity).OfType<LinkedUser>().Single().ExternalId;

        try
        {
            await desk.LeaveAccountAsync(int.Parse(deskUserId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ChatwootFailed(ex, cancellationToken))
        {
            log.LogWarning(ex, "Desk user {DeskUserId} joined for a person deleted meanwhile and is still in the account.", deskUserId);
        }
    }

    /// <summary>
    /// A Chatwoot call failed: it did not answer, or answered something that is not the JSON expected.
    /// A cancel by the request is not a failure, and a database error is not Chatwoot's.
    /// </summary>
    private static bool ChatwootFailed(Exception ex, CancellationToken cancellationToken)
        => ex is not DbUpdateException && (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested);
}
