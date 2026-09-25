namespace SpiritAI.GoTo;

/// <summary>Registers everything Spirit needs to hear GoTo calls.</summary>
public static class GoToServiceCollectionExtensions
{
    /// <summary>
    /// Adds the GoTo HTTP clients, the call-event queue and its reader, and the job that keeps the
    /// webhook channel alive. The job is off until the webhook keys in <see cref="GoToOptions"/> are set.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where <see cref="GoToOptions.SectionName"/> is read from.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddGoTo(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddGoToHttpClients(configuration);

        services.AddSingleton<GoToCallEventQueue>();

        services.AddHostedService<GoToCallEventReader>();

        services.AddHostedService<GoToChannelKeeper>();

        return services;
    }
}
