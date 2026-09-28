using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.Twenty;

namespace SpiritAI.Hub;

/// <summary>
/// Externally linked personals.
/// </summary>
public sealed class LinkPerson(SpiritDbContext db, DeskUsers deskUsers, CrmUsers crmUsers)
{
    /// <summary>
    /// Creates <paramref name="app"/>'s user for a Person and records the link, or finishes a
    /// create a prior attempt left half done.
    /// </summary>
    /// <param name="personId">The Person's id in <c>neon_auth."user"</c>.</param>
    /// <param name="app">One of <see cref="HubApps"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns><see cref="LinkState.Ready"/>, since this only returns once the link is ready.</returns>
    /// <exception cref="KeyNotFoundException">No Person has <paramref name="personId"/>.</exception>
    /// <exception cref="EmailAlreadyUsedException">The app already has this Person's email.</exception>
    public Task<LinkState> RunAsync(Guid personId, string app, CancellationToken cancellationToken)
        => app switch
        {
            HubApps.Desk => DeskAsync(personId, cancellationToken),
            HubApps.Crm => CrmAsync(personId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(app), app, "unknown Hub app."),
        };

    /// <summary>Desk create order (hub spec, section 5.3): resumes at whichever step did not finish.</summary>
    private async Task<LinkState> DeskAsync(Guid personId, CancellationToken cancellationToken)
    {
        var link = await db.LinkedUsers
            .SingleOrDefaultAsync(l => l.UserId == personId && l.App == HubApps.Desk, cancellationToken)
            .ConfigureAwait(false);

        if (link is { Ready: true })
        {
            return LinkState.Ready;
        }

        link ??= await CreateDeskUserAsync(personId, cancellationToken).ConfigureAwait(false);

        var externalId = int.Parse(link.ExternalId);
        await deskUsers.JoinAccountAsync(externalId, cancellationToken).ConfigureAwait(false);
        await deskUsers.JoinInboxAsync(externalId, cancellationToken).ConfigureAwait(false);

        link.Ready = true;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return LinkState.Ready;
    }

    /// <summary>
    /// Refuses an email Chatwoot already has, then makes the user and stores it, unfinished, at
    /// once. Two admins racing this at once collide on the primary key: a unique violation on the
    /// insert. The loser reloads and resumes with the row that won, rather than the user it made
    /// and now leaves behind unjoined. Any other failure propagates.
    /// </summary>
    private async Task<LinkedUser> CreateDeskUserAsync(Guid personId, CancellationToken cancellationToken)
    {
        var (name, email) = await PersonAsync(personId, cancellationToken).ConfigureAwait(false);

        if (await deskUsers.EmailIsUsedAsync(email, cancellationToken).ConfigureAwait(false))
        {
            throw new EmailAlreadyUsedException("Desk");
        }

        var externalId = await deskUsers.CreateUserAsync(name, email, cancellationToken).ConfigureAwait(false);

        var link = new LinkedUser { UserId = personId, App = HubApps.Desk, ExternalId = externalId.ToString(), Ready = false };
        db.LinkedUsers.Add(link);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(link).State = EntityState.Detached;

            link = await db.LinkedUsers
                .SingleAsync(l => l.UserId == personId && l.App == HubApps.Desk, cancellationToken)
                .ConfigureAwait(false);
        }

        return link;
    }

    /// <summary>CRM create: one step, so there is no half-done state to resume.</summary>
    private async Task<LinkState> CrmAsync(Guid personId, CancellationToken cancellationToken)
    {
        var link = await db.LinkedUsers
            .SingleOrDefaultAsync(l => l.UserId == personId && l.App == HubApps.Crm, cancellationToken)
            .ConfigureAwait(false);

        if (link is { Ready: true })
        {
            return LinkState.Ready;
        }

        var (name, email) = await PersonAsync(personId, cancellationToken).ConfigureAwait(false);
        var externalId = await crmUsers.CreateUserAsync(name, email, cancellationToken).ConfigureAwait(false);

        if (link is null)
        {
            db.LinkedUsers.Add(new LinkedUser { UserId = personId, App = HubApps.Crm, ExternalId = externalId, Ready = true });
        }
        else
        {
            link.ExternalId = externalId;
            link.Ready = true;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return LinkState.Ready;
    }

    /// <summary>Reads a Person's name and email straight off Neon Auth's table.</summary>
    private async Task<(string Name, string Email)> PersonAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await db.Database
            .SqlQuery<PersonNameEmail>($"""SELECT name AS "Name", email AS "Email" FROM neon_auth."user" WHERE id = {personId}""")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (person is null)
        {
            throw new KeyNotFoundException($"no Person has id {personId}.");
        }

        return (person.Name, person.Email);
    }

    private sealed record PersonNameEmail(string Name, string Email);
}
