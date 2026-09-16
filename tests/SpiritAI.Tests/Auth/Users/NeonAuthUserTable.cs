using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;

using Xunit;

namespace SpiritAI.Tests.Auth.Users;

/// <summary>
/// A <c>neon_auth."user"</c> in the throwaway PostgreSQL, shaped like the one Neon Auth keeps
/// (columns read off the live table on 2026-09-11), and rows in it that a test can own.
/// </summary>
internal static class NeonAuthUserTable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Makes the schema and the table when the database has neither.</summary>
    public static Task EnsureAsync(SpiritDbContext database)
        => database.Database.ExecuteSqlRawAsync(
            """
            CREATE SCHEMA IF NOT EXISTS neon_auth;
            CREATE TABLE IF NOT EXISTS neon_auth."user" (
                id              uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                name            text        NOT NULL,
                email           text        NOT NULL,
                "emailVerified" boolean     NOT NULL DEFAULT false,
                image           text,
                "createdAt"     timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "updatedAt"     timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
                role            text,
                banned          boolean,
                "banReason"     text,
                "banExpires"    timestamptz
            );
            """,
            Cancel);

    /// <summary>Writes one person, and returns their id.</summary>
    public static async Task<Guid> InsertAsync(SpiritDbContext database, string name, string email, bool? banned = null)
    {
        var id = Guid.NewGuid();

        await database.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, banned) VALUES ({id}, {name}, {email}, {banned})""",
            Cancel);

        return id;
    }

    /// <summary>Removes one person.</summary>
    public static Task DeleteAsync(SpiritDbContext database, Guid id)
        => database.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {id}""", Cancel);
}
