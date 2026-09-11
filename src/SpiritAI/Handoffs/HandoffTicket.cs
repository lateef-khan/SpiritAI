namespace SpiritAI.Handoffs;

/// <summary>
/// What an ask comes back with: the open row, and where the chat stands in the line.
/// </summary>
/// <param name="Row">The open handoff for the chat, whether this ask made it or an earlier one did.</param>
/// <param name="Position">
/// One for the front of the line. Zero when the row is already with a person: there is no line to
/// stand in.
/// </param>
public sealed record HandoffTicket(Handoff Row, int Position);
