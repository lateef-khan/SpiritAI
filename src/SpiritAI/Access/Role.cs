namespace SpiritAI.Access;

/// <summary>A named set of permissions that people are given.</summary>
public sealed class Role
{
    public Guid Id { get; set; }

    /// <summary>Unique without regard to case.</summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// <see langword="true"/> for <see cref="AdminRole"/> alone. It holds every permission in
    /// <see cref="Permissions.All"/>, none of them stored, and cannot be renamed, edited or deleted.
    /// </summary>
    public bool BuiltIn { get; set; }
}
