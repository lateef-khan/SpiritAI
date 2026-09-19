namespace SpiritAI.Handoffs.RealTime;

/// <summary>The groups a handoff push is addressed to.</summary>
public static class HandoffGroups
{
    /// <summary>Every member of staff on a socket.</summary>
    public const string Staff = "handoff:staff";

    /// <summary>Every visitor on a socket, whatever chat they are in.</summary>
    public const string Visitors = "handoff:visitors";

    /// <summary>What the group of one chat starts with.</summary>
    public const string ConversationPrefix = "call:";

    /// <summary>The visitor of one chat.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <returns>The group's name.</returns>
    public static string ForConversation(string conversationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        return ConversationPrefix + conversationId;
    }

    /// <summary>Whether a group is the visitor of some chat.</summary>
    /// <param name="group">The group's name.</param>
    /// <returns><see langword="true"/> for any <see cref="ForConversation"/> group.</returns>
    public static bool IsConversation(string group)
        => group.Length > ConversationPrefix.Length && group.StartsWith(ConversationPrefix, StringComparison.Ordinal);
}
