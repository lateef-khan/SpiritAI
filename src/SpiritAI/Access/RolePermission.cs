namespace SpiritAI.Access;

/// <summary>One permission a role grants, by its key. A key the code no longer has is ignored.</summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    /// <summary>One of <see cref="Permissions.KeyOf"/>.</summary>
    public required string Key { get; set; }
}
