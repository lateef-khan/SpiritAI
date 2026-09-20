using Microsoft.AspNetCore.Mvc;

namespace SpiritAI.Threads;

/// <summary>
/// One page of a thread's words, as the query string spells it. Bound with <c>[AsParameters]</c>
/// so the three routes that page words take one argument, not two, and the document still names
/// both fields.
/// </summary>
/// <param name="Before">
/// The cursor a previous page answered with, or <see langword="null"/> for the newest page. It
/// is a turn index, but the browser passes it back unread.
/// </param>
/// <param name="Limit">How many turns, or <see langword="null"/> for <see cref="HistoryWindow.DefaultTurns"/>.</param>
public sealed record HistoryQuery(
    [FromQuery(Name = "before")] string? Before,
    [FromQuery(Name = "limit")] int? Limit);
