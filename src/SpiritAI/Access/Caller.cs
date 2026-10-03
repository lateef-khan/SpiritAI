using System.Security.Claims;

namespace SpiritAI.Access;

/// <summary>Who asks for a change, and what they hold.</summary>
public sealed record Caller(Guid Id, IReadOnlySet<Permission> Held)
{
    /// <summary>The signed-in caller, or <see langword="null"/> when the token's subject is not a Neon user id.</summary>
    public static Caller? Of(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? new Caller(id, Permissions.Of(user)) : null;
    }
}
