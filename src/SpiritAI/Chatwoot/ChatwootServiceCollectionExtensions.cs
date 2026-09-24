using Microsoft.Extensions.DependencyInjection.Extensions;

using SpiritAI.Handoffs.Desk;
using SpiritAI.RealTime;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Registers the Chatwoot webhook, the copy of widget chats into Chatwoot, the visitor's typing,
/// and who of staff is online.
/// </summary>
public static class ChatwootServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="ChatwootOptions"/>, the webhook's event queue, worker, and handler, and
    /// the copy's client, queue, and worker, the catch-up of the AI's copy, the typing's queue, which hears the socket, and its
    /// worker, and the <see cref="IStaffPresence"/> read from Chatwoot's agents.
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

        services.AddScoped<ChatwootCatchUp>();

        services.AddSingleton<ChatwootCopyQueue>();

        services.AddScoped<ChatwootHandoffNotice>();

        services.AddScoped<ChatwootCopy>();

        services.AddHostedService<ChatwootCopyWorker>();

        services.AddSingleton<ChatwootTypingQueue>();

        services.AddSingleton<IRealTimeSignalListener>(provider => provider.GetRequiredService<ChatwootTypingQueue>());

        services.AddScoped<ChatwootTypingSender>();

        services.AddHostedService<ChatwootTypingWorker>();

        services.AddScoped<IStaffPresence, ChatwootStaffPresence>();

        return services;
    }
}
