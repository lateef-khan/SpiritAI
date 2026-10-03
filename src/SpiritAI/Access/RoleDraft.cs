namespace SpiritAI.Access;

/// <summary>A role as the Roles page makes or edits it.</summary>
/// <param name="Name">Trimmed before it is saved; unique without regard to case.</param>
/// <param name="Description">Optional; blank is saved as none.</param>
/// <param name="Permissions">What the role grants; repeats fold into one.</param>
public sealed record RoleDraft(string Name, string? Description, IReadOnlyList<Permission> Permissions);
