namespace SpiritAI.Handoffs.Model;

/// <summary>
/// One request for a person on one chat: <c>spirit.handoff</c>.
/// </summary>
public sealed class Handoff
{
    /// <summary>The database's own number for the row.</summary>
    public long Id { get; set; }

    /// <summary>The chat this is about, in AgentCore's <c>public.call</c>.</summary>
    public required string CallId { get; set; }

    /// <summary>Waiting for a person, with one, or done.</summary>
    public HandoffStatus Status { get; set; }

    /// <summary>Who asked.</summary>
    public HandoffAskedBy AskedBy { get; set; }

    /// <summary>What the asker said the person is for, when they said.</summary>
    public string? Reason { get; set; }

    /// <summary>When the ask was made. The database fills it in when left unset.</summary>
    public DateTimeOffset AskedAt { get; set; }

    /// <summary>The caller key of the member of staff who took the chat.</summary>
    public string? AssigneeKey { get; set; }

    /// <summary>The name the visitor sees above that person's replies.</summary>
    public string? AssigneeName { get; set; }

    /// <summary>When the chat was taken.</summary>
    public DateTimeOffset? ClaimedAt { get; set; }

    /// <summary>Where a reply goes when the visitor is not there to read it.</summary>
    public string? Email { get; set; }

    /// <summary>The last moment the visitor's socket was seen.</summary>
    public DateTimeOffset? VisitorSeenAt { get; set; }

    /// <summary>When the chat was handed back to the bot.</summary>
    public DateTimeOffset? DoneAt { get; set; }
}
