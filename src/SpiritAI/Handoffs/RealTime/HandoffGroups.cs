namespace SpiritAI.Handoffs.RealTime;

/// <summary>The groups a handoff push is addressed to.</summary>
public static class HandoffGroups
{
    /// <summary>Every member of staff on a socket.</summary>
    public const string Staff = "handoff:staff";

    /// <summary>Every visitor on a socket, whatever chat they are in.</summary>
    public const string Visitors = "handoff:visitors";

    /// <summary>What the group of one chat starts with.</summary>
    public const string CallPrefix = "call:";

    /// <summary>The visitor of one chat.</summary>
    /// <param name="callId">The chat.</param>
    /// <returns>The group's name.</returns>
    public static string ForCall(string callId)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        return CallPrefix + callId;
    }

    /// <summary>Whether a group is the visitor of some chat.</summary>
    /// <param name="group">The group's name.</param>
    /// <returns><see langword="true"/> for any <see cref="ForCall"/> group.</returns>
    public static bool IsCall(string group)
        => group.Length > CallPrefix.Length && group.StartsWith(CallPrefix, StringComparison.Ordinal);
}
