using Microsoft.Extensions.DependencyInjection.Extensions;

using SpiritAI.Chatwoot;
using SpiritAI.Twenty;

namespace SpiritAI.Hub;

/// <summary>Registers the Hub's tiles and its apps' sign-in.</summary>
public static class HubServiceCollectionExtensions
{
    /// <summary>
    /// Hub dependency injection.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">
    /// Where <see cref="HubOptions.SectionName"/> and <see cref="TwentyOptions.SectionName"/> are
    /// read from.
    /// </param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddHub(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<HubOptions>(configuration.GetSection(HubOptions.SectionName));
        services.Configure<TwentyOptions>(configuration.GetSection(TwentyOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<DeskUsers>();
        services.AddHttpClient<CrmUsers>();
        services.AddScoped<LinkPerson>();

        return services;
    }
}
