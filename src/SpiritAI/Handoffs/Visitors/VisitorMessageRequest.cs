namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// What the visitor says while a chat is waiting or with a person.
/// </summary>
/// <param name="Text">The words. Blank is refused.</param>
public sealed record VisitorMessageRequest(string Text);
