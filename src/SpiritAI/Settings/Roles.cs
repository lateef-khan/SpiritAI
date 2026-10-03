using Microsoft.EntityFrameworkCore;

using SpiritAI.Access;
using SpiritAI.Database;

namespace SpiritAI.Settings;

/// <summary>Reads roles with their permissions and member counts, the built-in Admin first.</summary>
public sealed class Roles(SpiritDbContext db, ILogger<Roles> log)
{
    public Task<IReadOnlyList<RoleRow>> ListAsync(CancellationToken cancellationToken) => RowsAsync(null, cancellationToken);

    public async Task<RoleRow?> OneAsync(Guid id, CancellationToken cancellationToken)
        => (await RowsAsync(id, cancellationToken).ConfigureAwait(false)).SingleOrDefault();

    private async Task<IReadOnlyList<RoleRow>> RowsAsync(Guid? only, CancellationToken cancellationToken)
    {
        var roles = await db.Roles
            .Where(role => only == null || role.Id == only)
            .OrderByDescending(role => role.BuiltIn)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = roles.Select(role => role.Id).ToList();

        var keys = (await db.RolePermissions.Where(p => ids.Contains(p.RoleId)).ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(p => p.RoleId, p => p.Key);

        var members = await db.UserRoles
            .Where(grant => ids.Contains(grant.RoleId))
            .GroupBy(grant => grant.RoleId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. roles.Select(role => new RoleRow(
                role.Id,
                role.Name,
                role.Description,
                role.BuiltIn,
                HeldPermissions.Read(role.BuiltIn, keys[role.Id], log),
                members.GetValueOrDefault(role.Id))),
        ];
    }
}
