namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// An email the visitor leaves on a chat that waits for a person.
/// </summary>
/// <param name="Email">The visitor's address. One that is not an address is refused.</param>
public sealed record VisitorEmailRequest(string Email);
