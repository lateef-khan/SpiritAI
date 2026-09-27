using System.Security.Claims;

using SpiritAI.Access;

namespace SpiritAI.Tests.Access;

/// <summary>Callers as the Neon scheme and the access claims leave them.</summary>
internal static class AccessTestUsers
{
    /// <summary>A signed-in caller before the access claims are added.</summary>
    public static ClaimsPrincipal SignedIn(Guid userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "neon"));

    /// <summary>A signed-in caller who holds these groups.</summary>
    public static ClaimsPrincipal Holding(params AccessGroup[] groups)
    {
        var user = SignedIn(Guid.NewGuid());
        user.AddIdentity(new ClaimsIdentity(
            groups.Select(group => new Claim(ClaimTypes.Role, group.ToString())),
            AccessGroups.IdentityType,
            ClaimTypes.Name,
            ClaimTypes.Role));
        return user;
    }
}
