using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// What the desk answers an ask with: the open row, and whether this ask is the one that made it.
/// </summary>
/// <param name="Row">The open handoff for the chat, whether this ask made it or an earlier one did.</param>
/// <param name="Created">
/// <see langword="true"/> when the chat asked for a person on this ask. <see langword="false"/> when
/// it was already open, so a second tap of the button changes nothing.
/// </param>
public sealed record HandoffAsked(Handoff Row, bool Created);
