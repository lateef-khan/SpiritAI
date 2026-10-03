using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;

namespace SpiritAI.Access;

/// <summary>
/// Handles writes to roles, permissions, and ban.
/// </summary>
public sealed class AccessWriter(SpiritDbContext db, AccessCache cache, TimeProvider clock)
{
    /// <summary>The migration's unique index on <c>lower(name)</c>.</summary>
    private const string NameIndex = "IX_role_name_lower";

    /// <summary>Whether every role exists and is the caller's to give. Writes nothing.</summary>
    public async Task<AccessWrite> CheckRolesAsync(IReadOnlyList<Guid> roleIds, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var wanted = roleIds.Distinct().ToList();

        if (!await AllRolesExistAsync(wanted, cancellationToken).ConfigureAwait(false))
        {
            return AccessWrite.UnknownRoles;
        }

        return await RefuseBeyondAsync(wanted, caller, cancellationToken).ConfigureAwait(false) ?? AccessWrite.Done;
    }

    /// <summary>Whether the caller may take something from this person: not their own account, and nothing the person's roles give that the caller lacks. Writes nothing.</summary>
    public async Task<AccessWrite> CheckPersonAsync(Guid personId, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (personId == caller.Id)
        {
            return AccessWrite.NotYourOwn;
        }

        var roles = await RolesOfAsync(personId, cancellationToken).ConfigureAwait(false);
        return await RefuseBeyondAsync(roles, caller, cancellationToken).ConfigureAwait(false) ?? AccessWrite.Done;
    }

    /// <summary>Replaces a person's roles, all or nothing.</summary>
    public async Task<AccessWrite> SetRolesAsync(Guid personId, IReadOnlyList<Guid> roleIds, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var wanted = roleIds.Distinct().ToList();

        var result = await InTransactionAsync(async () =>
        {
            if (!await PersonExistsAsync(personId, cancellationToken).ConfigureAwait(false))
            {
                return AccessWrite.NoSuchPerson;
            }

            if (!await AllRolesExistAsync(wanted, cancellationToken).ConfigureAwait(false))
            {
                return AccessWrite.UnknownRoles;
            }

            var current = await RolesOfAsync(personId, cancellationToken).ConfigureAwait(false);
            List<Guid> touched = [.. wanted.Except(current).Union(current.Except(wanted))];

            if (await RefuseBeyondAsync(touched, caller, cancellationToken).ConfigureAwait(false) is { } beyond)
            {
                return beyond;
            }

            if (current.Contains(AdminRole.Id) && !wanted.Contains(AdminRole.Id))
            {
                if (personId == caller.Id)
                {
                    return AccessWrite.OwnAdmin;
                }

                if (await WouldLeaveNoAdminAsync(personId, cancellationToken).ConfigureAwait(false))
                {
                    return AccessWrite.NoAdminLeft;
                }
            }

            await db.UserRoles.Where(grant => grant.UserId == personId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            db.UserRoles.AddRange(wanted.Select(roleId => new UserRole { UserId = personId, RoleId = roleId }));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return AccessWrite.Done;
        }, cancellationToken).ConfigureAwait(false);

        return await AfterAsync(result, personId).ConfigureAwait(false);
    }

    /// <summary>Bans a person. A second ban of a banned person changes nothing and is <see cref="AccessWrite.Done"/>.</summary>
    public async Task<AccessWrite> BanAsync(Guid personId, string? reason, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var result = await InTransactionAsync(async () =>
        {
            if (await RefusePersonAsync(personId, caller, cancellationToken).ConfigureAwait(false) is { } refused)
            {
                return refused;
            }

            if (!await db.PersonBans.AnyAsync(ban => ban.UserId == personId, cancellationToken).ConfigureAwait(false))
            {
                db.PersonBans.Add(new PersonBan
                {
                    UserId = personId,
                    BannedAt = clock.GetUtcNow(),
                    BannedBy = caller.Id,
                    Reason = Blank(reason),
                });
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return AccessWrite.Done;
        }, cancellationToken).ConfigureAwait(false);

        return await AfterAsync(result, personId).ConfigureAwait(false);
    }

    /// <summary>Lifts a ban. Only a caller who holds everything the person's roles give may lift it.</summary>
    public async Task<AccessWrite> UnbanAsync(Guid personId, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var result = await InTransactionAsync(async () =>
        {
            if (!await PersonExistsAsync(personId, cancellationToken).ConfigureAwait(false))
            {
                return AccessWrite.NoSuchPerson;
            }

            var roles = await RolesOfAsync(personId, cancellationToken).ConfigureAwait(false);
            if (await RefuseBeyondAsync(roles, caller, cancellationToken).ConfigureAwait(false) is { } beyond)
            {
                return beyond;
            }

            await db.PersonBans.Where(ban => ban.UserId == personId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            return AccessWrite.Done;
        }, cancellationToken).ConfigureAwait(false);

        return await AfterAsync(result, personId).ConfigureAwait(false);
    }

    public async Task<(AccessWrite Result, Guid RoleId)> CreateRoleAsync(RoleDraft draft, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(caller);

        var name = draft.Name.Trim();
        if (name.Length == 0)
        {
            return (AccessWrite.Invalid, Guid.Empty);
        }

        if (Exceeds(draft.Permissions.Select(Permissions.KeyOf), caller))
        {
            return (AccessWrite.BeyondYourAccess, Guid.Empty);
        }

        var roleId = Guid.CreateVersion7();

        var result = await InTransactionAsync(async () =>
        {
            db.Roles.Add(new Role { Id = roleId, Name = name, Description = Blank(draft.Description) });
            db.RolePermissions.AddRange(KeysOf(roleId, draft));
            return await SaveRoleAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return (result, roleId);
    }

    /// <summary>Renames a role and replaces its permissions. A key the code no longer has is dropped.</summary>
    public async Task<AccessWrite> UpdateRoleAsync(Guid roleId, RoleDraft draft, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(caller);

        var name = draft.Name.Trim();
        if (name.Length == 0)
        {
            return AccessWrite.Invalid;
        }

        var result = await InTransactionAsync(async () =>
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken).ConfigureAwait(false);
            if (role is null)
            {
                return AccessWrite.NoSuchRole;
            }

            if (role.BuiltIn)
            {
                return AccessWrite.BuiltIn;
            }

            var old = await KeysOfRoleAsync(roleId, cancellationToken).ConfigureAwait(false);
            if (Exceeds(old.Concat(draft.Permissions.Select(Permissions.KeyOf)), caller))
            {
                return AccessWrite.BeyondYourAccess;
            }

            role.Name = name;
            role.Description = Blank(draft.Description);
            await db.RolePermissions.Where(p => p.RoleId == roleId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            db.RolePermissions.AddRange(KeysOf(roleId, draft));

            return await SaveRoleAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return await AfterAsync(result, personId: null).ConfigureAwait(false);
    }

    /// <summary>Deletes a role; its members lose what it gave them.</summary>
    public async Task<AccessWrite> DeleteRoleAsync(Guid roleId, Caller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var result = await InTransactionAsync(async () =>
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken).ConfigureAwait(false);
            if (role is null)
            {
                return AccessWrite.NoSuchRole;
            }

            if (role.BuiltIn)
            {
                return AccessWrite.BuiltIn;
            }

            if (Exceeds(await KeysOfRoleAsync(roleId, cancellationToken).ConfigureAwait(false), caller))
            {
                return AccessWrite.BeyondYourAccess;
            }

            await db.Roles.Where(r => r.Id == roleId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            return AccessWrite.Done;
        }, cancellationToken).ConfigureAwait(false);

        return await AfterAsync(result, personId: null).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs one write as one retriable transaction. Every write takes the built-in Admin row's lock
    /// first, so two writes that would each leave one admin cannot both pass the last-admin check.
    /// </summary>
    private Task<AccessWrite> InTransactionAsync(Func<Task<AccessWrite>> work, CancellationToken cancellationToken)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM spirit.role WHERE id = {AdminRole.Id} FOR UPDATE", cancellationToken).ConfigureAwait(false);

            var result = await work().ConfigureAwait(false);

            if (result == AccessWrite.Done)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        });

    /// <summary>
    /// Runs once a write has committed: drops one person's cached access, or everyone's when a role
    /// changed (<paramref name="personId"/> null).
    /// </summary>
    private async Task<AccessWrite> AfterAsync(AccessWrite result, Guid? personId)
    {
        if (result != AccessWrite.Done)
        {
            return result;
        }

        if (personId is { } one)
        {
            await cache.ForgetAsync(one, CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            await cache.ForgetEveryoneAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>The guards a ban must pass, in the order their refusals are reported.</summary>
    private async Task<AccessWrite?> RefusePersonAsync(Guid personId, Caller caller, CancellationToken cancellationToken)
    {
        if (!await PersonExistsAsync(personId, cancellationToken).ConfigureAwait(false))
        {
            return AccessWrite.NoSuchPerson;
        }

        if (personId == caller.Id)
        {
            return AccessWrite.NotYourOwn;
        }

        if (await RefuseBeyondAsync(await RolesOfAsync(personId, cancellationToken).ConfigureAwait(false), caller, cancellationToken).ConfigureAwait(false) is { } beyond)
        {
            return beyond;
        }

        return await WouldLeaveNoAdminAsync(personId, cancellationToken).ConfigureAwait(false) ? AccessWrite.NoAdminLeft : null;
    }

    /// <summary>Refuses when the roles hold a permission the caller does not; the Admin role holds them all.</summary>
    private async Task<AccessWrite?> RefuseBeyondAsync(IReadOnlyCollection<Guid> roleIds, Caller caller, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return null;
        }

        IEnumerable<string> keys = roleIds.Contains(AdminRole.Id)
            ? Permissions.All.Select(info => Permissions.KeyOf(info.Key))
            : await db.RolePermissions.Where(p => roleIds.Contains(p.RoleId)).Select(p => p.Key)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Exceeds(keys, caller) ? AccessWrite.BeyondYourAccess : null;
    }

    /// <summary>Whether a key names a permission the caller lacks. A key the code no longer has is nobody's, so it never refuses.</summary>
    private static bool Exceeds(IEnumerable<string> keys, Caller caller)
        => keys.Any(key => Permissions.TryParse(key, out var permission) && !caller.Held.Contains(permission));

    /// <summary>Whether this person is the only Admin holder who is not banned.</summary>
    private async Task<bool> WouldLeaveNoAdminAsync(Guid personId, CancellationToken cancellationToken)
    {
        var admins = await db.UserRoles
            .Where(grant => grant.RoleId == AdminRole.Id && !db.PersonBans.Any(ban => ban.UserId == grant.UserId))
            .Select(grant => grant.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return admins is [var only] && only == personId;
    }

    private Task<bool> PersonExistsAsync(Guid personId, CancellationToken cancellationToken)
        => db.Database.SqlQuery<bool>($"""SELECT EXISTS (SELECT 1 FROM neon_auth."user" WHERE id = {personId}) AS "Value" """)
            .SingleAsync(cancellationToken);

    private async Task<bool> AllRolesExistAsync(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
        => await db.Roles.CountAsync(role => roleIds.Contains(role.Id), cancellationToken).ConfigureAwait(false) == roleIds.Count;

    private Task<List<Guid>> RolesOfAsync(Guid personId, CancellationToken cancellationToken)
        => db.UserRoles.Where(grant => grant.UserId == personId).Select(grant => grant.RoleId).ToListAsync(cancellationToken);

    private Task<List<string>> KeysOfRoleAsync(Guid roleId, CancellationToken cancellationToken)
        => db.RolePermissions.Where(p => p.RoleId == roleId).Select(p => p.Key).ToListAsync(cancellationToken);

    private async Task<AccessWrite> SaveRoleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return AccessWrite.Done;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NameIndex })
        {
            return AccessWrite.NameTaken;
        }
    }

    private static IEnumerable<RolePermission> KeysOf(Guid roleId, RoleDraft draft)
        => draft.Permissions.Distinct().Select(permission => new RolePermission { RoleId = roleId, Key = Permissions.KeyOf(permission) });

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
