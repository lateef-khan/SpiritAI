namespace SpiritAI.Handoffs;

/// <summary>
/// How a claim went.
/// </summary>
public enum HandoffClaimResult
{
    /// <summary>The chat is now this member of staff's.</summary>
    Won,

    /// <summary>Somebody else got there first; the row names them.</summary>
    AlreadyTaken,

    /// <summary>The chat has no open handoff to take.</summary>
    NotWaiting,
}
