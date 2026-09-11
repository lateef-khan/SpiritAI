using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;

namespace SpiritAI.Auth.Users;

/// <summary>
/// The <see cref="IUserDirectory"/> over the <c>neon_auth."user"</c> table Neon Auth keeps in the
/// same database as everything else. Read only; Neon owns the rows. A banned person is nobody
/// here: <c>banned</c> is nullable and unset for most rows, so it is tested as "not true".
/// </summary>
public sealed class NeonUserDirectory(SpiritDbContext database) : IUserDirectory
{
    /// <inheritdoc />
    public async ValueTask<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var row = await database.Database
            .SqlQuery<Row>(
                $"""
                SELECT id::text AS "Id", name AS "Name", email AS "Email"
                FROM neon_auth."user"
                WHERE lower(email) = lower({email}) AND banned IS NOT TRUE
                LIMIT 1
                """)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : new AuthUser(row.Id, row.Name, row.Email);
    }

    /// <summary>What one query row lands in. EF binds the columns by these names.</summary>
    private sealed class Row
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
}
