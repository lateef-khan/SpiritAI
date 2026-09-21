using Microsoft.Extensions.Caching.Hybrid;

namespace SpiritAI.Auth.Users;

/// <summary>Registers the directory of people with a Neon sign-in.</summary>
public static class NeonUsersServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IUserDirectory"/> over the <c>neon_auth."user"</c> table, behind the host's
    /// cache. Add it after <c>AddSpiritDatabase</c> and <c>AddSpiritCache</c>: the directory reads
    /// through the same context, and remembers through the same cache.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddNeonUsers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<NeonUserDirectory>();

        services.AddScoped<IUserDirectory>(provider => new CachedUserDirectory(
            provider.GetRequiredService<NeonUserDirectory>(),
            provider.GetRequiredService<HybridCache>()));

        return services;
    }
}
