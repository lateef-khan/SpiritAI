using Microsoft.Extensions.Caching.Hybrid;

namespace SpiritAI.Access;

/// <summary>Drops cached access, so a change counts on the person's next request.</summary>
public sealed class AccessCache(HybridCache cache)
{
    /// <summary>Forgets what is cached for <paramref name="userId"/> on this app machine.</summary>
    public ValueTask ForgetAsync(Guid userId, CancellationToken cancellationToken)
        => cache.RemoveAsync(AccessClaimsTransformation.KeyOf(userId), cancellationToken);

    /// <summary>Forgets everyone's cached access, after a change to a role many people may hold.</summary>
    public ValueTask ForgetEveryoneAsync(CancellationToken cancellationToken)
        => cache.RemoveByTagAsync(AccessClaimsTransformation.AccessTag, cancellationToken);
}
