namespace SpiritAI.Auth.Users;

/// <summary>Registers the directory of people with a Neon sign-in.</summary>
public static class NeonUsersServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IUserDirectory"/> over the <c>neon_auth."user"</c> table. Add it after
    /// <c>AddSpiritDatabase</c>: the directory reads through the same context.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddNeonUsers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IUserDirectory, NeonUserDirectory>();

        return services;
    }
}
