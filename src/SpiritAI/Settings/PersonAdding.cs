using Microsoft.EntityFrameworkCore;

using SpiritAI.Access;
using SpiritAI.Database;
using SpiritAI.Hub;
using SpiritAI.Neon;
using SpiritAI.Twenty;

namespace SpiritAI.Settings;

/// <summary>
/// Adds a person in one call.
/// </summary>
public sealed class PersonAdding(
    SpiritDbContext db,
    NeonUsers neon,
    AccessWriter writer,
    LinkPerson linker,
    People people,
    ILogger<PersonAdding> log)
{
    /// <param name="name">Trimmed.</param>
    /// <param name="email">Trimmed and lower-cased.</param>
    public async Task<AddOutcome> AddAsync(
        string name, string email, IReadOnlyList<Guid> roleIds, bool desk, bool crm, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var check = await writer.CheckRolesAsync(roleIds, caller, cancellationToken).ConfigureAwait(false);
        if (check != AccessWrite.Done)
        {
            return AddOutcome.Refuse(check);
        }

        var existing = await db.Database
            .SqlQuery<Guid>($"""SELECT id AS "Value" FROM neon_auth."user" WHERE lower(email) = {email}""")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid id;

        if (existing.Count > 1)
        {
            return AddOutcome.Taken;
        }

        if (existing is [var found])
        {
            if (await db.UserRoles.AnyAsync(grant => grant.UserId == found, cancellationToken).ConfigureAwait(false))
            {
                return AddOutcome.Taken;
            }

            if (await db.PersonBans.AnyAsync(ban => ban.UserId == found, cancellationToken).ConfigureAwait(false))
            {
                return AddOutcome.BannedPerson;
            }

            id = found;
        }
        else
        {
            try
            {
                id = await neon.CreateAsync(email, name, cancellationToken).ConfigureAwait(false);
            }
            catch (NeonEmailTakenException)
            {
                return AddOutcome.Taken;
            }
            catch (NeonUnavailableException ex)
            {
                return AddOutcome.Made(new AddedPerson(null, new AddSteps(StepResult.Failed, StepResult.None, StepResult.None, StepResult.None, ex.Message)));
            }
        }

        List<string> problems = [];

        var rolesStep = StepResult.Done;
        
        if (await writer.SetRolesAsync(id, roleIds, caller, cancellationToken).ConfigureAwait(false) != AccessWrite.Done)
        {
            problems.Add("The roles were not saved.");
            rolesStep = StepResult.Failed;
        }

        var deskStep = desk ? await LinkAsync(id, HubApps.Desk, "Desk", problems, cancellationToken).ConfigureAwait(false) : StepResult.None;
        var crmStep = crm ? await LinkAsync(id, HubApps.Crm, "CRM", problems, cancellationToken).ConfigureAwait(false) : StepResult.None;

        var steps = new AddSteps(StepResult.Done, rolesStep, deskStep, crmStep, problems.Count == 0 ? null : string.Join(" ", problems));

        return AddOutcome.Made(new AddedPerson(await people.OneAsync(id, cancellationToken).ConfigureAwait(false), steps));
    }

    private async Task<StepResult> LinkAsync(Guid id, string app, string label, List<string> problems, CancellationToken cancellationToken)
    {
        try
        {
            await linker.RunAsync(id, app, cancellationToken).ConfigureAwait(false);
            return StepResult.Done;
        }
        catch (Exception ex) when (ex is HttpRequestException or CrmUnavailableException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            log.LogWarning(ex, "Add could not link {App} for {PersonId}.", app, id);
            problems.Add(ex is CrmUnavailableException ? ex.Message : $"{label} did not answer.");
            return StepResult.Failed;
        }
    }
}
