using SpiritAI.Handoffs.Store;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// What the desk answers an ask with: the ticket, and whether this ask is the one that made the row.
/// </summary>
/// <param name="Ticket">The open row and its place in the line.</param>
/// <param name="Created">
/// <see langword="true"/> when the chat joined the queue on this ask. <see langword="false"/> when
/// it was already open, so a second tap of the button changes nothing.
/// </param>
public sealed record HandoffAsked(HandoffTicket Ticket, bool Created);
