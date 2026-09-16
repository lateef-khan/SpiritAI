namespace SpiritAI.Handoffs.Contracts;

/// <summary>
/// Where a waiting chat stands in the line, as <c>handoff.queue</c> carries it.
/// </summary>
/// <param name="CallId">The chat.</param>
/// <param name="Position">One for the front.</param>
public sealed record HandoffQueuePosition(string CallId, int Position);
