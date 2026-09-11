namespace SpiritAI.Handoffs;

/// <summary>
/// Where a handoff is in its life. Stored as lowercase text: <c>waiting</c>, <c>human</c>, <c>done</c>.
/// </summary>
public enum HandoffStatus
{
    /// <summary>A person was asked for and nobody has taken the chat yet.</summary>
    Waiting,

    /// <summary>A member of staff claimed the chat and is the one talking.</summary>
    Human,

    /// <summary>Closed. The row stays as the wait-time record; the bot has the chat again.</summary>
    Done,
}
