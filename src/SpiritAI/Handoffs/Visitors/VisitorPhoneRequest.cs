namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// A phone number the visitor leaves on a chat that waits for a person, for staff to call back.
/// </summary>
/// <param name="Phone">The number as the visitor typed it. One that is not a valid number is refused.</param>
public sealed record VisitorPhoneRequest(string Phone);
