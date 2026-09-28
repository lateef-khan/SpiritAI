using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;

namespace SpiritAI.Access;

/// <summary>
/// Adds one <see cref="ClaimTypes.Role"/> claim per access group the signed-in user holds.
/// </summary>
internal sealed class AccessClaimsTransformation(IServiceScopeFactory scopes, HybridCache cache) : IClaimsTransformation
{
    /// <summary>How long a user's groups are trusted.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static readonly HybridCacheEntryOptions Write = new() { Expiration = Lifetime };

    /// <summary>The cache key of one user's groups.</summary>
    public static string KeyOf(Guid userId) => $"spirit:access:{userId}";

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identities.Any(identity => identity.AuthenticationType == AccessGroups.IdentityType)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return principal;
        }

        var groups = await cache.GetOrCreateAsync(
            KeyOf(userId),
            (scopes, userId),
            static async (state, cancellationToken) =>
            {
                await using var scope = state.scopes.CreateAsyncScope();
                var access = scope.ServiceProvider.GetRequiredService<IUserAccess>();
                return (await access.GroupsOfAsync(state.userId, cancellationToken).ConfigureAwait(false)).ToArray();
            },
            Write).ConfigureAwait(false);

        principal.AddIdentity(new ClaimsIdentity(
            groups.Select(group => new Claim(ClaimTypes.Role, group.ToString())),
            AccessGroups.IdentityType,
            ClaimTypes.Name,
            ClaimTypes.Role));

        return principal;
    }
}
