namespace SpiritAI.Access;

/// <summary>One role held by one Neon Auth user.</summary>
public sealed class UserRole
{
    /// <summary>The user's id in <c>neon_auth."user"</c>, which is also the token's <c>sub</c>.</summary>
    public Guid UserId { get; set; }

    /// <summary>The <see cref="Access.Role.Name"/> held.</summary>
    public required string Role { get; set; }
}
