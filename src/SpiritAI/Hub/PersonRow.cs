using SpiritAI.Access;

namespace SpiritAI.Hub;

/// <summary>One row of the Settings people table.</summary>
/// <param name="Id">The Person's id in <c>neon_auth."user"</c>.</param>
/// <param name="Name">The Person's name, as Neon Auth has it.</param>
/// <param name="Email">The Person's email, as Neon Auth has it.</param>
/// <param name="Groups">The access groups <c>spirit.user_role</c> grants this Person.</param>
/// <param name="Desk">Whether this Person can sign in to Desk.</param>
/// <param name="Crm">Whether this Person can sign in to CRM.</param>
public sealed record PersonRow(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<AccessGroup> Groups,
    LinkState Desk,
    LinkState Crm);
