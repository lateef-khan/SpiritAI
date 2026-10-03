using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;

namespace SpiritAI.Access;

/// <summary>Reads a person's ban, roles and role permissions in one query.</summary>
internal sealed class AccessResolver(SpiritDbContext db, ILogger<AccessResolver> log) : IAccessResolver
{
    /// <inheritdoc />
    public async ValueTask<PersonAccess> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await db.Database
            .SqlQuery<AccessRow>($"""
                SELECT
                    EXISTS (SELECT 1 FROM spirit.person_ban WHERE user_id = {userId}) AS "Banned",
                    EXISTS (SELECT 1 FROM spirit.user_role ur JOIN spirit.role r ON r.id = ur.role_id
                            WHERE ur.user_id = {userId} AND r.built_in) AS "Admin",
                    ARRAY(SELECT DISTINCT rp.permission FROM spirit.user_role ur
                          JOIN spirit.role_permission rp ON rp.role_id = ur.role_id
                          WHERE ur.user_id = {userId}) AS "Keys"
                """)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var held = HeldPermissions.Read(row.Admin, row.Keys, log);

        return new PersonAccess(row.Banned, held, Permissions.AgentOf(held));
    }

    private sealed record AccessRow(bool Banned, bool Admin, string[] Keys);
}
