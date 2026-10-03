namespace SpiritAI.Settings;

/// <summary>A person to add.</summary>
/// <param name="Name">Their name; trimmed.</param>
/// <param name="Email">Their email; trimmed and lower-cased.</param>
/// <param name="RoleIds">The roles to give them.</param>
/// <param name="Desk">Whether to link Desk too.</param>
/// <param name="Crm">Whether to link CRM too.</param>
public sealed record AddPersonBody(string Name, string Email, IReadOnlyList<Guid> RoleIds, bool Desk, bool Crm);
