namespace SpiritAI.RealTime;

/// <summary>The events the hub itself pushes. Features name their own.</summary>
public static class RealTimeEvents
{
    /// <summary>How many callers of one kind are online. Carries a <see cref="RealTimePresence"/>.</summary>
    public const string Presence = "presence";

    /// <summary>One socket said something to a group. Carries a <see cref="RealTimeSignal"/>.</summary>
    public const string Signal = "signal";
}
