namespace SpiritAI.Handoffs;

/// <summary>
/// Who may work the handoff queue, bound from the <see cref="SectionName"/> section.
/// </summary>
/// <remarks>
/// A settings file rather than a table, because the team is a handful of people and a Neon sign-in
/// already proves who they are. The list only has to say which of the signed-in are staff, and what
/// the visitor should call them. A users table replaces this the day there is one.
/// </remarks>
public sealed class HandoffOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "Handoff";

    /// <summary>
    /// Who may work the queue, and the name the visitor sees. Empty means nobody: a signed-in dealer
    /// is not staff.
    /// </summary>
    public HandoffStaffMember[] Staff { get; set; } = [];
}
