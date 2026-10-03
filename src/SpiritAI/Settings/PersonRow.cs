using SpiritAI.Access;
using SpiritAI.Hub;

namespace SpiritAI.Settings;

/// <summary>One row of the Settings people table.</summary>
/// <param name="Id">The Person's id in <c>neon_auth."user"</c>.</param>
/// <param name="Name">The Person's name, as Neon Auth has it.</param>
/// <param name="Email">The Person's email, as Neon Auth has it.</param>
/// <param name="Roles">The roles they hold, by name.</param>
/// <param name="Agent">The chat agent their roles give, or <see langword="null"/>.</param>
/// <param name="Banned">Whether Spirit bans them.</param>
/// <param name="Desk">Their Desk link.</param>
/// <param name="Crm">Their CRM link.</param>
public sealed record PersonRow(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<RoleRef> Roles,
    Permission? Agent,
    bool Banned,
    LinkState Desk,
    LinkState Crm);
