namespace SpiritAI.Access;

/// <summary>A job role from the access sheet, and the access group it grants.</summary>
public sealed class Role
{
    /// <summary>The role's name, as the sheet spells it.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The <see cref="AccessGroup"/> name, or <see langword="null"/> when the role gives no agent.
    /// </summary>
    public string? AccessGroup { get; set; }
}
