using System.Security.Claims;

namespace SpiritAI.Tests.Access;

/// <summary>Callers as the Neon scheme and the access claims leave them.</summary>
internal static class AccessTestUsers
{
    /// <summary>A signed-in caller before the access claims are added.</summary>
    public static ClaimsPrincipal SignedIn(Guid userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "neon"));
}
