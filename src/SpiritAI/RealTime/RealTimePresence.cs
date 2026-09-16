namespace SpiritAI.RealTime;

/// <summary>
/// How many callers of one kind are on a socket, as <see cref="RealTimeEvents.Presence"/> carries it.
/// </summary>
/// <param name="Kind">Which kind of caller, as the admission that let them in named it.</param>
/// <param name="Online">The count of distinct callers, not sockets.</param>
public sealed record RealTimePresence(string Kind, int Online);
