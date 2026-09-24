using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Registers what the AI turn needs of Chatwoot.
/// </summary>
public static class ChatwootServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="ChatwootOptions"/>, the client, the catch-up of the AI's copy, and the
    /// posting of its answers.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where <see cref="ChatwootOptions.SectionName"/> is read from.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddChatwoot(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ChatwootOptions>(configuration.GetSection(ChatwootOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient<ChatwootClient>();

        services.AddScoped<ChatwootCatchUp>();

        services.AddScoped<ChatwootAnswer>();

        return services;
    }
}
