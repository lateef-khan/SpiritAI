namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// Where a reply goes when the visitor is not there to read it.
/// </summary>
/// <param name="Email">The visitor's address. One that is not an address is refused.</param>
public sealed record VisitorEmailRequest(string Email);
