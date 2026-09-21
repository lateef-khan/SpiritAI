using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Store;

/// <summary>
/// One page of a listing, and where the next one starts.
/// </summary>
/// <param name="Rows">The page, in the view's order.</param>
/// <param name="Next">The cursor for the page after this one, or <see langword="null"/> when this is the last.</param>
public sealed record HandoffListing(IReadOnlyList<Handoff> Rows, HandoffCursor? Next);
