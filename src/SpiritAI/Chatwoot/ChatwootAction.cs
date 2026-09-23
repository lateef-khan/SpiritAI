namespace SpiritAI.Chatwoot;

/// <summary>What Spirit does about one Chatwoot webhook.</summary>
public enum ChatwootAction
{
    /// <summary>Nothing: the event is not about a Spirit chat, or a person did not cause it.</summary>
    Ignore,

    /// <summary>A member of staff wrote to the visitor.</summary>
    StaffMessage,

    /// <summary>A member of staff has the chat.</summary>
    Assigned,

    /// <summary>The chat is closed, and goes back to the bot.</summary>
    Resolved,

    /// <summary>A member of staff started typing to the visitor.</summary>
    TypingOn,

    /// <summary>A member of staff stopped typing to the visitor.</summary>
    TypingOff,
}
