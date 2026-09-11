namespace SpiritAI.Handoffs;

/// <summary>
/// One person allowed to take a chat, as <see cref="HandoffOptions.Staff"/> lists them.
/// </summary>
public sealed class HandoffStaffMember
{
    /// <summary>The address on their Neon sign-in. Matched without regard to case.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>What the visitor sees above their replies.</summary>
    public string Name { get; set; } = string.Empty;
}
