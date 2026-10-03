using Microsoft.EntityFrameworkCore;

using SpiritAI.Hub;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Callback;

/// <summary>
/// Dana, who owns GoTo line 8625 (<c>GoTo/Payloads/admin_users.json</c>: dana@spiritfitness.com),
/// as a Spirit person with a Desk link to Chatwoot user 3.
/// </summary>
internal static class DeskStaffSeed
{
    public const string ChatwootUserId = "3";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Dana with her Desk link. Her Neon email is in other letter cases than GoTo's, on purpose.</summary>
    public static async Task DanaAsync(PostgresFixture fixture, bool ready = true)
    {
        await NoDanaAsync(fixture);

        var id = Guid.NewGuid();
        await using var db = fixture.Open();
        await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({id}, 'Dana Test', 'Dana@SpiritFitness.com', true)""",
            Cancel);
        db.LinkedUsers.Add(new LinkedUser { UserId = id, App = HubApps.Desk, ExternalId = ChatwootUserId, Ready = ready });
        await db.SaveChangesAsync(Cancel);
    }

    /// <summary>No Dana in Spirit, and no one linked to Chatwoot user 3.</summary>
    public static async Task NoDanaAsync(PostgresFixture fixture)
    {
        await using var db = fixture.Open();
        await db.LinkedUsers.Where(link => link.App == HubApps.Desk && link.ExternalId == ChatwootUserId).ExecuteDeleteAsync(Cancel);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE lower(email) = 'dana@spiritfitness.com'""", Cancel);
    }
}
