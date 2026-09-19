namespace SpiritAI.Handoffs.Store;

/// <summary>
/// Which end of a listing comes first. The open views sort on the ask; done sorts on the close.
/// </summary>
public enum HandoffOrder
{
    /// <summary>The order the queue is served in.</summary>
    OldestFirst,

    /// <summary>The chats just asked, or just finished, at the top.</summary>
    NewestFirst,
}
