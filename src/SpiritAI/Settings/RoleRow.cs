using SpiritAI.Access;

namespace SpiritAI.Settings;

/// <summary>One role on the Roles page.</summary>
/// <param name="BuiltIn">The Admin role: every permission, and locked.</param>
/// <param name="Permissions">What it grants, in <see cref="Access.Permissions.All"/> order.</param>
/// <param name="Members">How many people hold it.</param>
public sealed record RoleRow(Guid Id, string Name, string? Description, bool BuiltIn, IReadOnlyList<Permission> Permissions, int Members);
