namespace SpiritAI.Handoffs;

/// <summary>
/// Which side asked for a person. Stored as lowercase text: <c>bot</c>, <c>visitor</c>.
/// </summary>
public enum HandoffAskedBy
{
    /// <summary>The agent called its <c>request_human</c> tool.</summary>
    Bot,

    /// <summary>The visitor tapped the button in the widget.</summary>
    Visitor,
}
