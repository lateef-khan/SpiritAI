using Microsoft.EntityFrameworkCore;

using SpiritAI.Access;
using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.Hub;
using SpiritAI.Neon;
using SpiritAI.Twenty;

namespace SpiritAI.Settings;

/// <summary>
/// Deletes a person.
/// </summary>
public sealed class PersonRemoval(SpiritDbContext db, DeskUsers desk, CrmUsers crm, NeonUsers neon, AccessCache cache, ILogger<PersonRemoval> log)
{
    public async Task<PersonDeletion> DeleteAsync(Guid personId, CancellationToken cancellationToken)
    {
        var links = await db.LinkedUsers.Where(link => link.UserId == personId).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<string> problems = [];

        var deskResult = StepResult.None;
        if (links.SingleOrDefault(link => link.App == HubApps.Desk) is { } deskLink)
        {
            try
            {
                await desk.LeaveAccountAsync(int.Parse(deskLink.ExternalId), cancellationToken).ConfigureAwait(false);
                await DropAsync(deskLink, cancellationToken).ConfigureAwait(false);
                deskResult = StepResult.Done;
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                log.LogWarning(ex, "Could not take Desk user {DeskUserId} out of the account.", deskLink.ExternalId);
                deskResult = StepResult.Failed;
                problems.Add("Desk did not answer.");
            }
        }

        var crmResult = StepResult.None;
        if (links.SingleOrDefault(link => link.App == HubApps.Crm) is { } crmLink)
        {
            try
            {
                await crm.DeleteUserAsync(crmLink.ExternalId, cancellationToken).ConfigureAwait(false);
                await DropAsync(crmLink, cancellationToken).ConfigureAwait(false);
                crmResult = StepResult.Done;
            }
            catch (Exception ex) when (ex is CrmUnavailableException or CrmRefusedException)
            {
                crmResult = StepResult.Failed;
                problems.Add(ex.Message);
            }
        }

        var neonResult = StepResult.Failed;

        if (deskResult != StepResult.Failed && crmResult != StepResult.Failed)
        {
            try
            {
                await neon.DeleteAsync(personId, cancellationToken).ConfigureAwait(false);
                await cache.ForgetAsync(personId, CancellationToken.None).ConfigureAwait(false);
                neonResult = StepResult.Done;
            }
            catch (NeonUnavailableException ex)
            {
                problems.Add(ex.Message);
            }
        }
        else
        {
            problems.Add("Spirit keeps the sign-in until Desk and CRM are done. Try again.");
        }

        return new PersonDeletion(deskResult, crmResult, neonResult, problems.Count == 0 ? null : string.Join(" ", problems));
    }

    /// <exception cref="HttpRequestException">Chatwoot refused.</exception>
    /// <exception cref="TaskCanceledException">Chatwoot timed out.</exception>
    /// <exception cref="CrmRefusedException">The member is Twenty's last admin.</exception>
    /// <exception cref="CrmUnavailableException">Twenty could not be reached.</exception>
    public async Task<bool> UnlinkAsync(Guid personId, string app, CancellationToken cancellationToken)
    {
        var link = await db.LinkedUsers.SingleOrDefaultAsync(l => l.UserId == personId && l.App == app, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return false;
        }

        if (app == HubApps.Desk)
        {
            await desk.LeaveAccountAsync(int.Parse(link.ExternalId), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await crm.DeleteUserAsync(link.ExternalId, cancellationToken).ConfigureAwait(false);
        }

        await DropAsync(link, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task DropAsync(LinkedUser link, CancellationToken cancellationToken)
    {
        db.LinkedUsers.Remove(link);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
