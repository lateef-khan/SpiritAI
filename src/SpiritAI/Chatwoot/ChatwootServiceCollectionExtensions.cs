using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SpiritAI.Chatwoot;

/// <summary>Registers the Chatwoot webhook and the copy of widget chats into Chatwoot.</summary>
public static class ChatwootServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="ChatwootOptions"/>, the webhook's event queue, worker, and handler, and
    /// the copy's client, queue, and worker. Add it after <c>AddSpiritCache</c>, whose cache the
    /// webhook dedupes with, <c>AddSpiritDatabase</c> and <c>AddContacts</c>, which the copy reads,
    /// and <c>AddHandoffs</c>, <c>AddHandoffMail</c>, and <c>AddRealTime</c>, whose ports the
    /// handler uses.
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

        services.AddSingleton<ChatwootEventQueue>();

        services.AddScoped<ChatwootEventHandler>();

        services.AddHostedService<ChatwootEventWorker>();

        services.AddHttpClient<ChatwootClient>();

        services.AddSingleton<ChatwootCopyQueue>();

        services.AddScoped<ChatwootHandoffNotice>();

        services.AddScoped<ChatwootCopy>();

        services.AddHostedService<ChatwootCopyWorker>();

        return services;
    }
}
