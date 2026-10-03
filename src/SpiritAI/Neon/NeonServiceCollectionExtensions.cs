namespace SpiritAI.Neon;

/// <summary>Registers the server's Neon client.</summary>
public static class NeonServiceCollectionExtensions
{
    /// <param name="services">The host's services. Needs <c>AddSpiritDatabase</c>.</param>
    /// <param name="configuration">Where <see cref="NeonOptions.SectionName"/> is read from.</param>
    public static IServiceCollection AddNeon(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<NeonOptions>(configuration.GetSection(NeonOptions.SectionName));
        services.AddHttpClient<NeonUsers>();

        return services;
    }
}
