using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;

namespace SpiritAI.Access;

/// <summary>Reads a user's groups from <c>spirit.user_role</c> joined to <c>spirit.role</c>.</summary>
internal sealed class DatabaseUserAccess(SpiritDbContext db, ILogger<DatabaseUserAccess> log) : IUserAccess
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AccessGroup>> GroupsOfAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var names = await db.UserRoles
            .Where(grant => grant.UserId == userId)
            .Join(db.Roles, grant => grant.Role, role => role.Name, (_, role) => role.AccessGroup)
            .Where(group => group != null)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AccessGroup> groups = [];

        foreach (var name in names)
        {
            // The check constraint names every group, so this only fires when the enum and the
            // constraint drift apart.
            if (Enum.TryParse<AccessGroup>(name, out var group) && Enum.IsDefined(group))
            {
                groups.Add(group);
            }
            else
            {
                log.LogWarning("spirit.role names the access group {AccessGroup}, which the code does not know. It is skipped.", name);
            }
        }

        return groups;
    }
}
