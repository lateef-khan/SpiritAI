using SpiritAI.Handoffs.Contracts;

namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// The rows of one state, as the inbox lists them.
/// </summary>
/// <param name="Items">Oldest ask first for the open states; newest close first for the closed.</param>
public sealed record HandoffPage(IReadOnlyList<HandoffSummary> Items);
