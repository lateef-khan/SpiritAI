namespace SpiritAI.Handoffs;

/// <summary>
/// One person allowed to take a chat, as <see cref="Staff.StaffGate"/> admits them.
/// </summary>
/// <param name="Email">The address on their Neon sign-in.</param>
/// <param name="Name">What the visitor sees above their replies.</param>
public sealed record HandoffStaffMember(string Email, string Name);
