using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;

namespace SpiritAI.Access;

/// <summary>Adds one <see cref="Permissions.ClaimType"/> claim per permission the signed-in person holds.</summary>
internal sealed class AccessClaimsTransformation(IServiceScopeFactory scopes, HybridCache cache) : IClaimsTransformation
{
    /// <summary>How long a person's access is trusted.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>The tag every person's cached access carries, so a role change can drop them all.</summary>
    public const string AccessTag = "spirit:access";

    private static readonly HybridCacheEntryOptions Write = new() { Expiration = Lifetime };

    private static readonly string[] Tags = [AccessTag];

    /// <summary>The cache key of one person's access.</summary>
    public static string KeyOf(Guid userId) => $"spirit:access:v3:{userId}";

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identities.Any(identity => identity.AuthenticationType == Permissions.IdentityType)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return principal;
        }

        var access = await cache.GetOrCreateAsync(
            KeyOf(userId),
            (scopes, userId),
            static async (state, cancellationToken) =>
            {
                await using var scope = state.scopes.CreateAsyncScope();
                var resolver = scope.ServiceProvider.GetRequiredService<IAccessResolver>();
                return await resolver.ResolveAsync(state.userId, cancellationToken).ConfigureAwait(false);
            },
            Write,
            Tags).ConfigureAwait(false);

        Claim[] claims = access.Banned
            ? [new Claim(Permissions.BannedClaimType, "true")]
            : [.. access.Held.Select(permission => new Claim(Permissions.ClaimType, Permissions.KeyOf(permission)))];

        principal.AddIdentity(new ClaimsIdentity(claims, Permissions.IdentityType, ClaimTypes.Name, ClaimTypes.Role));

        return principal;
    }
}
