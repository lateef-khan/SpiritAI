namespace SpiritAI.Settings;

/// <summary>The roles a person is to hold; repeats fold into one.</summary>
public sealed record RolesBody(IReadOnlyList<Guid> RoleIds);
