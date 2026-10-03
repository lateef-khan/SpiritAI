using System.Globalization;

using Microsoft.EntityFrameworkCore;

using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.GoTo;

namespace SpiritAI.Handoffs.Callback;

/// <summary>
/// Finds the Chatwoot agent behind a GoTo line: the line's owner in GoTo, matched by email to a
/// Spirit person, then that person's ready Desk link.
/// </summary>
public sealed class CallStaff(SpiritDbContext db, GoToStaffDirectory directory)
{
    /// <summary>The agent whose line it is.</summary>
    /// <param name="line">The staff line in the call.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The agent, or <see langword="null"/> when the line's owner is no Spirit person with a ready Desk link.</returns>
    public async Task<ChatwootAgent?> FindAgentAsync(GoToCallLine line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (await directory.FindEmailAsync(line.LineId, cancellationToken).ConfigureAwait(false) is not { Length: > 0 } email)
        {
            return null;
        }

        var staff = await db.Database
            .SqlQuery<DeskStaff>($"""
                SELECT l.external_id AS "ExternalId", u.name AS "Name", u.email AS "Email"
                FROM neon_auth."user" u
                JOIN spirit.linked_user l ON l.user_id = u.id AND l.app = 'desk' AND l.ready
                WHERE lower(u.email) = lower({email})
                """)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return staff is null ? null : new ChatwootAgent(int.Parse(staff.ExternalId, CultureInfo.InvariantCulture), staff.Name, staff.Email);
    }

    private sealed record DeskStaff(string ExternalId, string Name, string Email);
}
