namespace SpiritAI.Handoffs.Notifications;

/// <summary>
/// The names of the handoff pushes in section 6.2 of the spec, spelled the way the browser
/// subscribes to them. Typing is not here: it is a generic signal named <c>typing</c>.
/// </summary>
public static class HandoffEvents
{
    /// <summary>A chat joined the queue. Carries a <c>HandoffSummary</c>.</summary>
    public const string Waiting = "handoff.waiting";

    /// <summary>A waiting chat's place in the line moved. Carries a <c>HandoffQueuePosition</c>.</summary>
    public const string Queue = "handoff.queue";

    /// <summary>A member of staff took a chat. Carries the chat and the assignee.</summary>
    public const string Claimed = "handoff.claimed";

    /// <summary>A chat went back to the bot. Carries the chat.</summary>
    public const string Done = "handoff.done";

    /// <summary>A message landed in a chat of the human phase. Carries a <c>HandoffMessage</c>.</summary>
    public const string MessageCreated = "message.created";
}
