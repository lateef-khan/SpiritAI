using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// Where one chat stands, as the visitor sees it: the truth the widget fetches after a reconnect.
/// </summary>
/// <param name="Status">
/// <c>bot</c> when nobody was ever asked for, <c>waiting</c>, <c>human</c>, or <c>done</c> once the
/// last person left.
/// </param>
/// <param name="AssigneeName">The name of the person holding the chat, while one does.</param>
/// <param name="StaffOnline">
/// Whether anyone on staff is on a socket right now. A yes or no, never a count: how many people
/// are behind the desk is nothing a stranger's page should be told.
/// </param>
/// <param name="Phone">
/// The phone number the visitor left, in international form, once they left one. A reloaded widget
/// reads it to know it need not ask again.
/// </param>
/// <param name="Code">
/// The reference code staff know the handoff by, while it is open. The visitor gives it when they
/// call first.
/// </param>
public sealed record HandoffState(string Status, string? AssigneeName, bool StaffOnline, string? Phone, long? Code)
{
    /// <summary>The status of a chat with no handoff row at all.</summary>
    public const string Bot = "bot";

    /// <summary>Describes one chat to its visitor.</summary>
    /// <param name="row">The chat's latest row, or <see langword="null"/> when nobody was ever asked for.</param>
    /// <param name="staffOnline">How many members of staff are on a socket.</param>
    /// <returns>The state.</returns>
    public static HandoffState Of(Handoff? row, int staffOnline)
    {
        if (row is null)
        {
            return new HandoffState(Bot, null, staffOnline > 0, Phone: null, Code: null);
        }

        var open = row.Status != HandoffStatus.Done;

        return new HandoffState(
            row.Status.ToString().ToLowerInvariant(),
            row.Status == HandoffStatus.Human ? row.AssigneeName : null,
            staffOnline > 0,
            open && row.Phone is { } phone ? VisitorPhone.Display(phone) : null,
            open ? row.Id : null);
    }
}
