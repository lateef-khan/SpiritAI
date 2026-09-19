using SpiritAI.Handoffs.Contracts;

namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// One page of the rows of one view, as the inbox lists them.
/// </summary>
/// <param name="Items">Oldest ask first for the open views; newest close first for the closed.</param>
/// <param name="NextCursor">
/// What to send as <c>cursor</c> for the page after this one, or <see langword="null"/> when this
/// is the last. Opaque: the host alone reads it.
/// </param>
public sealed record HandoffPage(IReadOnlyList<HandoffSummary> Items, string? NextCursor);
