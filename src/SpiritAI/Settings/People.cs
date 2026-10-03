using Microsoft.EntityFrameworkCore;

using SpiritAI.Access;
using SpiritAI.Database;
using SpiritAI.Hub;

namespace SpiritAI.Settings;

/// <summary>
/// Reads the People table's rows straight off <c>neon_auth."user"</c> and our own tables.
/// </summary>
public sealed class People(SpiritDbContext db, ILogger<People> log)
{
    /// <summary>Every Person, with their roles, agent, ban and link states.</summary>
    public async Task<IReadOnlyList<PersonRow>> ListAsync(CancellationToken cancellationToken)
        => await RowsAsync(null, cancellationToken).ConfigureAwait(false);

    /// <summary>One Person's row, or <see langword="null"/> when there is no such Person.</summary>
    public async Task<PersonRow?> OneAsync(Guid id, CancellationToken cancellationToken)
        => (await RowsAsync(id, cancellationToken).ConfigureAwait(false)).SingleOrDefault();

    private async Task<IReadOnlyList<PersonRow>> RowsAsync(Guid? only, CancellationToken cancellationToken)
    {
        var people = only is { } one
            ? await db.Database
                .SqlQuery<PersonRecord>($"""
                    SELECT u.id AS "Id", u.name AS "Name", u.email AS "Email",
                           EXISTS (SELECT 1 FROM spirit.person_ban b WHERE b.user_id = u.id) AS "Banned"
                    FROM neon_auth."user" u WHERE u.id = {one}
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : await db.Database
                .SqlQuery<PersonRecord>($"""
                    SELECT u.id AS "Id", u.name AS "Name", u.email AS "Email",
                           EXISTS (SELECT 1 FROM spirit.person_ban b WHERE b.user_id = u.id) AS "Banned"
                    FROM neon_auth."user" u ORDER BY u.name
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var grants = await db.UserRoles
            .Where(grant => only == null || grant.UserId == only)
            .Join(db.Roles, grant => grant.RoleId, role => role.Id, (grant, role) => new { grant.UserId, role.Id, role.Name, role.BuiltIn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roleIds = grants.Select(grant => grant.Id).Distinct().ToList();

        var keysByRole = (await db.RolePermissions
                .Where(permission => roleIds.Contains(permission.RoleId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(permission => permission.RoleId, permission => permission.Key);

        var links = await db.LinkedUsers
            .Where(link => only == null || link.UserId == only)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grantsByPerson = grants.ToLookup(grant => grant.UserId);
        
        var linksByPerson = links.ToLookup(link => link.UserId);

        return
        [
            .. people.Select(person =>
            {
                var held = grantsByPerson[person.Id];
                var permissions = HeldPermissions.Read(
                    held.Any(grant => grant.BuiltIn), held.SelectMany(grant => keysByRole[grant.Id]), log);

                return new PersonRow(
                    person.Id,
                    person.Name,
                    person.Email,
                    [.. held.Select(grant => new RoleRef(grant.Id, grant.Name)).OrderBy(role => role.Name, StringComparer.Ordinal)],
                    Permissions.AgentOf(permissions),
                    person.Banned,
                    StateOf(linksByPerson[person.Id], HubApps.Desk),
                    StateOf(linksByPerson[person.Id], HubApps.Crm));
            }),
        ];
    }

    /// <summary>Whether a Person's link to one app is missing, unfinished, or ready.</summary>
    private static LinkState StateOf(IEnumerable<LinkedUser> links, string app)
        => links.SingleOrDefault(link => link.App == app) switch
        {
            null => LinkState.None,
            { Ready: true } => LinkState.Ready,
            _ => LinkState.Unfinished,
        };

    /// <summary>The columns of <c>neon_auth."user"</c> the People list reads, and the ban.</summary>
    private sealed record PersonRecord(Guid Id, string Name, string Email, bool Banned);
}
