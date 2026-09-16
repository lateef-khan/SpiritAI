using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// Where one chat stands, as the visitor sees it: the truth the widget fetches after a reconnect.
/// </summary>
/// <param name="Status">
/// <c>bot</c> when nobody was ever asked for, <c>waiting</c>, <c>human</c>, or <c>done</c> once the
/// last person left.
/// </param>
/// <param name="Position">Where a waiting chat stands in the line, one for the front. Absent otherwise.</param>
/// <param name="AssigneeName">The name of the person holding the chat, while one does.</param>
/// <param name="StaffOnline">How many members of staff are on a socket right now.</param>
/// <param name="Email">
/// Where a reply goes when the visitor is away, once they left one. A reloaded widget reads it to
/// know it need not ask again.
/// </param>
public sealed record HandoffState(string Status, int? Position, string? AssigneeName, int StaffOnline, string? Email)
{
    /// <summary>The status of a chat with no handoff row at all.</summary>
    public const string Bot = "bot";

    /// <summary>Describes one chat to its visitor.</summary>
    /// <param name="row">The chat's latest row, or <see langword="null"/> when nobody was ever asked for.</param>
    /// <param name="position">Where a waiting row stands, or <see langword="null"/> when it is not waiting.</param>
    /// <param name="staffOnline">How many members of staff are on a socket.</param>
    /// <returns>The state.</returns>
    public static HandoffState Of(Handoff? row, int? position, int staffOnline)
        => row is null
            ? new HandoffState(Bot, null, null, staffOnline, Email: null)
            : new HandoffState(
                HandoffSummary.StatusOf(row.Status),
                row.Status == HandoffStatus.Waiting ? position : null,
                row.Status == HandoffStatus.Human ? row.AssigneeName : null,
                staffOnline,
                row.Status == HandoffStatus.Done ? null : row.Email);
}
