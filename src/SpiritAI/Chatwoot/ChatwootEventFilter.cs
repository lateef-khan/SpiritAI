namespace SpiritAI.Chatwoot;

/// <summary>
/// Decides what one webhook asks of Spirit. Only staff reach the visitor: the messages Spirit
/// copies into Chatwoot itself come back as webhooks too, sent by the contact or the bot, and are
/// dropped here.
/// </summary>
public static class ChatwootEventFilter
{
    /// <summary>What Spirit does about an event.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The action, <see cref="ChatwootAction.Ignore"/> for anything Spirit does not act on.</returns>
    public static ChatwootAction ActionOf(ChatwootEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.SpiritConversationId is null)
        {
            return ChatwootAction.Ignore;
        }

        return e.Name switch
        {
            "message_created" when IsStaffWordsToVisitor(e) => ChatwootAction.StaffMessage,
            "conversation_status_changed" when e.Status == "resolved" => ChatwootAction.Resolved,
            "conversation_status_changed" or "conversation_updated" when e.Status == "open" && e.AssigneeName is not null => ChatwootAction.Assigned,
            "conversation_typing_on" when IsStaffInTheReplyBox(e) => ChatwootAction.TypingOn,
            "conversation_typing_off" when IsStaffInTheReplyBox(e) => ChatwootAction.TypingOff,
            _ => ChatwootAction.Ignore,
        };
    }

    private static bool IsStaffWordsToVisitor(ChatwootEvent e)
        => e.MessageType == "outgoing"
            && !e.IsPrivate
            && e.ActorType == ChatwootEvent.StaffActor
            && !string.IsNullOrWhiteSpace(e.Content);

    private static bool IsStaffInTheReplyBox(ChatwootEvent e)
        => !e.IsPrivate && e.ActorType == ChatwootEvent.StaffActor;
}
